using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Processing;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>
/// Предварительный подбор: какие продукты подходят по цели, сумме, сроку и возрасту бизнеса, во что обойдётся каждый
/// и успеют ли деньги к нужной дате. Чистый расчёт без обращения к базе и скорингу; правила в docs/financing-preview.md.
/// </summary>
public sealed class FinancingAdvisor(FinancingPreviewOptions options, DecisionPolicyOptions policy)
{
    public const string Disclaimer =
        "Предварительный расчёт по базовым ставкам продуктов и данным компании из кабинета. Точные условия банк назначает после проверки заявки: " +
        "ставка зависит от оценки риска, сумма может быть снижена. Заявка не создана.";

    /// <summary>Доля выручки, принимаемая за прибыль, когда ни прибыль, ни расходы неизвестны (как в резервной балльной модели).</summary>
    public const decimal EstimatedProfitShareOfRevenue = 0.10m;

    /// <summary>Верхняя граница отображаемой нагрузки: выше неё точное число не информативно.</summary>
    public const decimal DebtLoadCap = 9.99m;

    /// <summary>Грейд, задающий верхнюю границу ставки при автоматическом одобрении.</summary>
    public const RiskGrade MaxAutoApprovalGrade = RiskGrade.C;

    /// <summary>Доля расходов в выручке, выше которой правила скоринга rules-0.3 снижают оценку (фактор expense_ratio).</summary>
    public const decimal ExpenseRatioHigh = 0.90m;

    private readonly FinancingPreviewOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly DecisionPolicyOptions _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    public FinancingPreview Build(Company company, IReadOnlyList<CreditProduct> products, FinancingPreviewQuery query, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(query);

        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var businessAgeMonths = company.GetBusinessAgeMonths(today);

        var options = new List<FinancingOption>();
        var unsuitable = new List<UnsuitableProduct>();
        foreach (var product in products.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.Ordinal))
        {
            var reasons = Mismatches(product, query, businessAgeMonths).ToList();
            if (reasons.Count > 0)
            {
                unsuitable.Add(new UnsuitableProduct(product, reasons));
                continue;
            }

            options.Add(BuildOption(company, product, query, today));
        }

        var ordered = Rank(options);
        var summary = ordered.Count > 0 ? OptionsSummary(ordered, products.Count) : NoOptionsSummary(unsuitable);

        return new FinancingPreview(
            now,
            company,
            businessAgeMonths,
            query,
            query.NeedByDate.DayNumber - today.DayNumber,
            summary,
            Disclaimer,
            ordered,
            unsuitable);
    }

    /// <summary>Те же проверки, что при подаче заявки: вариант из ответа проходит валидацию POST /api/applications.</summary>
    private static IEnumerable<PreviewNote> Mismatches(CreditProduct product, FinancingPreviewQuery query, int businessAgeMonths)
    {
        if (!product.IsActive)
        {
            yield return new PreviewNote("product_inactive", $"Продукт «{product.Name}» сейчас недоступен для оформления.");
        }

        if (!product.AllowsPurpose(query.Purpose))
        {
            var allowed = string.Join(", ", product.AllowedPurposes.Select(p => $"«{p.Title()}»"));
            yield return new PreviewNote("purpose_not_allowed", $"Цель «{query.Purpose.Title()}» недоступна. Доступные цели: {allowed}.");
        }

        if (query.Amount < product.MinAmount)
        {
            yield return new PreviewNote("amount_below_min", $"Минимальная сумма продукта {Money.FormatRub(product.MinAmount)}.");
        }
        else if (query.Amount > product.MaxAmount)
        {
            yield return new PreviewNote("amount_above_max", $"Максимальная сумма продукта {Money.FormatRub(product.MaxAmount)}.");
        }

        if (query.TermMonths is { } term && !product.AllowsTerm(term))
        {
            yield return new PreviewNote("term_out_of_range", $"Срок должен быть от {product.MinTermMonths} до {product.MaxTermMonths} мес.");
        }

        if (businessAgeMonths < product.MinBusinessAgeMonths)
        {
            yield return new PreviewNote("business_too_young", $"Возраст бизнеса {businessAgeMonths} мес., для продукта требуется не менее {product.MinBusinessAgeMonths} мес.");
        }
    }

    private FinancingOption BuildOption(Company company, CreditProduct product, FinancingPreviewQuery query, DateOnly today)
    {
        var termSuggested = query.TermMonths is null;
        var term = query.TermMonths ?? SuggestTerm(company, product, query.Amount);
        var cost = Cost(query.Amount, product.BaseRate, term);
        var affordability = Affordability(company, cost.MonthlyPayment);
        var funding = Funding(product, query, today);

        return new FinancingOption(
            product,
            IsRecommended: false,
            query.Amount,
            term,
            termSuggested,
            cost,
            affordability,
            funding,
            Explanations(product, funding, term, termSuggested).ToList(),
            Limitations(company, product, query, cost, affordability, funding).ToList(),
            Requirements(company, product, query).ToList());
    }

    /// <summary>
    /// Самый короткий срок с комфортной нагрузкой; иначе самый короткий без штрафа в скоринге (меньше 50 %); иначе меньше 70 %;
    /// иначе максимальный срок продукта. Границы строгие, как в банде фактора debt_load.
    /// </summary>
    private int SuggestTerm(Company company, CreditProduct product, decimal amount)
    {
        foreach (var threshold in new[] { _options.ComfortableDebtLoad, _options.ElevatedDebtLoad, _options.HighDebtLoad })
        {
            for (var term = product.MinTermMonths; term <= product.MaxTermMonths; term++)
            {
                var load = DebtLoad(company, LoanMath.MonthlyPayment(amount, product.BaseRate, term)).Load;
                if (load is { } value && value < threshold)
                {
                    return term;
                }
            }
        }

        return product.MaxTermMonths;
    }

    private static OptionCost Cost(decimal amount, decimal baseRate, int term)
    {
        var maxRate = baseRate + RiskGrades.RatePremium(MaxAutoApprovalGrade);
        var schedule = LoanMath.Schedule(amount, baseRate, term, DateOnly.MinValue.AddMonths(1));
        var maxSchedule = LoanMath.Schedule(amount, maxRate, term, DateOnly.MinValue.AddMonths(1));

        return new OptionCost(
            baseRate,
            maxRate,
            LoanMath.MonthlyPayment(amount, baseRate, term),
            LoanMath.MonthlyPayment(amount, maxRate, term),
            schedule.Sum(r => r.Payment),
            schedule.Sum(r => r.Interest),
            maxSchedule.Sum(r => r.Interest));
    }

    private static (decimal Profit, ProfitBasis Basis) MonthlyProfit(Company company)
    {
        if (company.NetProfit is { } netProfit)
        {
            return (netProfit / 12m, ProfitBasis.NetProfit);
        }

        if (company.AnnualExpenses is { } expenses)
        {
            return ((company.AnnualRevenue - expenses) / 12m, ProfitBasis.RevenueMinusExpenses);
        }

        return (company.AnnualRevenue * EstimatedProfitShareOfRevenue / 12m, ProfitBasis.RevenueEstimate);
    }

    /// <summary>Формула сервиса скоринга: (текущие платежи + новый платёж) / месячная прибыль. Без прибыли нагрузка не определена.</summary>
    private static (decimal? Load, decimal Profit, ProfitBasis Basis) DebtLoad(Company company, decimal monthlyPayment)
    {
        var (profit, basis) = MonthlyProfit(company);
        if (profit <= 0)
        {
            return (null, profit, basis);
        }

        return ((company.MonthlyDebtPayments + monthlyPayment) / profit, profit, basis);
    }

    private Affordability Affordability(Company company, decimal monthlyPayment)
    {
        var (load, profit, basis) = DebtLoad(company, monthlyPayment);
        var level = load switch
        {
            null => DebtLoadLevel.High,
            var value when value < _options.ComfortableDebtLoad => DebtLoadLevel.Comfortable,
            var value when value < _options.ElevatedDebtLoad => DebtLoadLevel.Moderate,
            var value when value < _options.HighDebtLoad => DebtLoadLevel.Elevated,
            _ => DebtLoadLevel.High,
        };

        var basisNote = basis switch
        {
            ProfitBasis.RevenueMinusExpenses => " Прибыль оценена как выручка минус расходы.",
            ProfitBasis.RevenueEstimate => $" Прибыль и расходы в карточке не указаны: прибыль принята как {Percent(EstimatedProfitShareOfRevenue)} выручки.",
            _ => string.Empty,
        };

        string explanation;
        if (load is null)
        {
            explanation = "По данным компании прибыли за последние 12 месяцев нет: банк оценит заявку с осторожностью, возможен отказ или снижение суммы." + basisNote;
        }
        else
        {
            var levelText = level switch
            {
                DebtLoadLevel.Comfortable => $"комфортная нагрузка (меньше {Percent(_options.ComfortableDebtLoad)})",
                DebtLoadLevel.Moderate => $"заметная, но посильная нагрузка ({PercentValue(_options.ComfortableDebtLoad)}–{Percent(_options.ElevatedDebtLoad)})",
                DebtLoadLevel.Elevated => $"повышенная нагрузка ({PercentValue(_options.ElevatedDebtLoad)}–{Percent(_options.HighDebtLoad)}): оценка риска снижается, условия могут быть хуже",
                _ => $"высокая нагрузка ({Percent(_options.HighDebtLoad)} и больше), возможен отказ или снижение суммы",
            };
            explanation = $"Платёж {Money.FormatRub(monthlyPayment)} вместе с текущими платежами {Money.FormatRub(company.MonthlyDebtPayments)} составит {Percent(load.Value)} месячной прибыли {Money.FormatRub(profit)}: {levelText}.{basisNote}";
        }

        return new Affordability(
            Math.Round(profit, 2),
            company.MonthlyDebtPayments,
            load is { } ratio ? Math.Min(Math.Round(ratio, 2), DebtLoadCap) : null,
            level,
            basis,
            explanation);
    }

    private FundingEstimate Funding(CreditProduct product, FinancingPreviewQuery query, DateOnly today)
    {
        var aboveAutoLimit = query.Amount > product.AutoApprovalLimit;
        var manual = product.RequiresCollateral || aboveAutoLimit;
        var businessDays = product.RequiresCollateral
            ? _options.CollateralBusinessDays
            : manual ? _options.ManualReviewBusinessDays : _options.AutoDecisionBusinessDays;
        var expected = BusinessCalendar.AddBusinessDays(today, businessDays);
        var inTime = expected <= query.NeedByDate;

        var reason = product.RequiresCollateral
            ? "Нужен залог, решение принимает менеджер"
            : aboveAutoLimit
                ? $"Сумма выше лимита автоодобрения {Money.FormatRub(product.AutoApprovalLimit)}, решение принимает менеджер"
                : $"Решение принимается автоматически, сумма в пределах лимита {Money.FormatRub(product.AutoApprovalLimit)}";
        var outcome = inTime
            ? $"до нужной даты {query.NeedByDate:dd.MM.yyyy} остаётся {RussianPlural.Count(query.NeedByDate.DayNumber - expected.DayNumber, "день", "дня", "дней")}"
            : $"это позже нужной даты {query.NeedByDate:dd.MM.yyyy}";

        return new FundingEstimate(
            manual ? FundingPath.ManualReview : FundingPath.Auto,
            businessDays,
            expected,
            inTime,
            $"{reason}: деньги ориентировочно {expected:dd.MM.yyyy}, {outcome}.");
    }

    private static IEnumerable<PreviewNote> Explanations(CreditProduct product, FundingEstimate funding, int term, bool termSuggested)
    {
        yield return new PreviewNote("product_about", product.Description);

        if (!product.RequiresCollateral)
        {
            yield return new PreviewNote("no_collateral", "Без залога.");
        }

        if (funding.DecisionPath == FundingPath.Auto)
        {
            yield return new PreviewNote("auto_decision", $"Решение принимается автоматически: сумма в пределах лимита автоодобрения {Money.FormatRub(product.AutoApprovalLimit)}.");
        }

        if (termSuggested)
        {
            yield return new PreviewNote("term_suggested", $"Срок {term} мес. подобран так, чтобы платёж оставался посильным при минимальной переплате; можно выбрать от {product.MinTermMonths} до {product.MaxTermMonths} мес.");
        }

        yield return new PreviewNote("data_prefilled", "Выручка, налоги и реквизиты подгружаются из кабинета автоматически, вводить их не нужно.");
    }

    private IEnumerable<PreviewNote> Limitations(Company company, CreditProduct product, FinancingPreviewQuery query, OptionCost cost, Affordability affordability, FundingEstimate funding)
    {
        yield return new PreviewNote("rate_depends_on_risk", $"Ставка от {Money.FormatRate(cost.InterestRate)} до {Money.FormatRate(cost.MaxInterestRate)} годовых: точную ставку банк назначает после оценки риска.");

        if (query.Amount > product.AutoApprovalLimit)
        {
            yield return new PreviewNote("manual_review_amount", $"Сумма выше лимита автоодобрения {Money.FormatRub(product.AutoApprovalLimit)}: решение принимает кредитный менеджер.");
        }

        if (product.RequiresCollateral)
        {
            yield return new PreviewNote("collateral_required", $"Нужен залог: его оформление и оценка занимают до {RussianPlural.Count(_options.CollateralBusinessDays, "рабочего дня", "рабочих дней", "рабочих дней")}.");
        }

        var cap = Math.Max(Math.Round(company.AnnualRevenue * _policy.MaxAmountShareOfRevenue, 0), product.MinAmount);
        if (query.Amount > cap)
        {
            yield return new PreviewNote("amount_may_be_reduced", $"При среднем уровне риска банк может снизить сумму до {Money.FormatRub(cap)} ({Percent(_policy.MaxAmountShareOfRevenue)} годовой выручки).");
        }

        if (company.AnnualExpenses is { } expenses && company.AnnualRevenue > 0 && expenses / company.AnnualRevenue > ExpenseRatioHigh)
        {
            yield return new PreviewNote("expense_ratio_high", $"Расходы составляют {Percent(expenses / company.AnnualRevenue)} выручки, больше {Percent(ExpenseRatioHigh)}: оценка риска снижается.");
        }

        if (affordability.Level == DebtLoadLevel.Elevated && affordability.DebtLoad is { } elevated)
        {
            yield return new PreviewNote("elevated_debt_load", $"Платёж вместе с текущими обязательствами составит {Percent(elevated)} месячной прибыли: оценка риска снижается, ставка может быть выше.");
        }

        if (affordability.Level == DebtLoadLevel.High)
        {
            yield return new PreviewNote(
                "high_debt_load",
                affordability.DebtLoad is { } load
                    ? $"Платёж вместе с текущими обязательствами составит {Percent(load)} месячной прибыли: возможен отказ или снижение суммы."
                    : "По данным компании прибыли нет: возможен отказ или снижение суммы.");
        }

        if (!funding.InTime)
        {
            yield return new PreviewNote("deadline_missed", $"Ориентир получения {funding.ExpectedDate:dd.MM.yyyy} позже нужной даты {query.NeedByDate:dd.MM.yyyy}.");
        }
    }

    private static IEnumerable<PreviewNote> Requirements(Company company, CreditProduct product, FinancingPreviewQuery query)
    {
        yield return new PreviewNote("credit_history_consent", "Согласие на запрос кредитной истории: без него заявку нельзя подать.");

        if (ValidationRules.NeedsFinancialStatements(query.Amount, product.Type) && (company.AnnualExpenses is null || company.NetProfit is null))
        {
            yield return new PreviewNote("financials_required", "Расходы и чистая прибыль за последние 12 месяцев: без них заявка остановится на уточнении данных.");
        }

        if (product.RequiresCollateral)
        {
            yield return new PreviewNote("collateral_documents", "Документы на предмет залога.");
        }
    }

    /// <summary>Сначала варианты, которые успевают к дате, затем по возрастанию переплаты. Первый рекомендуется, самый дешёвый получает пометку.</summary>
    private static List<FinancingOption> Rank(List<FinancingOption> options)
    {
        if (options.Count == 0)
        {
            return options;
        }

        var ordered = options
            .OrderByDescending(o => o.Funding.InTime)
            .ThenBy(o => o.Cost.TotalInterest)
            .ThenBy(o => o.Product.SortOrder)
            .ToList();

        var cheapest = ordered.MinBy(o => o.Cost.TotalInterest)!;
        var cheapestNote = new PreviewNote("cheapest", "Самая низкая переплата среди подходящих вариантов.");
        for (var i = 0; i < ordered.Count; i++)
        {
            var option = ordered[i];
            var explanations = ReferenceEquals(option, cheapest) ? [.. option.Explanations, cheapestNote] : option.Explanations;
            ordered[i] = option with { IsRecommended = i == 0, Explanations = explanations };
        }

        return ordered;
    }

    private static string OptionsSummary(IReadOnlyList<FinancingOption> ordered, int totalProducts)
    {
        var best = ordered[0];
        var verb = RussianPlural.Form(ordered.Count, "Подходит", "Подходят", "Подходят");
        var count = RussianPlural.Count(ordered.Count, "вариант", "варианта", "вариантов");
        var deadline = best.Funding.InTime ? "к нужной дате успеваем" : "это позже нужной даты";
        return $"{verb} {count} из {totalProducts}. Рекомендуем «{best.Product.Name}»: платёж {Money.FormatRub(best.Cost.MonthlyPayment)} в месяц, " +
               $"переплата {Money.FormatRub(best.Cost.TotalInterest)} за {best.TermMonths} мес., деньги ориентировочно {best.Funding.ExpectedDate:dd.MM.yyyy}, {deadline}.";
    }

    private static string NoOptionsSummary(IReadOnlyList<UnsuitableProduct> unsuitable)
    {
        if (unsuitable.Count == 0)
        {
            return "Подходящих продуктов нет: в каталоге нет активных продуктов.";
        }

        // Одинаковые причины у разных продуктов объединяются: «…не менее 6 мес. Продукты: «Экспресс-кредит», «Овердрафт».»
        var groups = unsuitable
            .SelectMany(u => u.Reasons.Select(r => (r.Text, u.Product.Name)))
            .GroupBy(x => x.Text, x => x.Name, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {(g.Count() == 1 ? "Продукт" : "Продукты")}: {string.Join(", ", g.Select(name => $"«{name}»"))}.");
        return $"Подходящих продуктов нет. {string.Join(" ", groups)}";
    }

    private static string Percent(decimal share) => $"{PercentValue(share)} %";

    private static string PercentValue(decimal share) => Math.Round(share * 100, 0, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
