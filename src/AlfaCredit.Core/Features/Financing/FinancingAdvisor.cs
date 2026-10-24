using System.Globalization;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Processing;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>
/// Предварительный подбор: какие срочные продукты подходят по цели, сумме, сроку и возрасту бизнеса, во что обойдётся каждый
/// и успеют ли деньги к нужной дате. Чистый расчёт без обращения к базе и скорингу; правила в docs/financing-preview.md.
/// </summary>
public sealed class FinancingAdvisor(FinancingPreviewOptions options, DecisionPolicyOptions policy)
{
    public const string Disclaimer =
        "Предварительный расчёт по базовым ставкам продуктов и данным компании из кабинета: не является одобрением и не гарантирует выдачу. " +
        "Точные условия банк назначает после проверки заявки: ставка зависит от оценки риска, сумма может быть снижена. Заявка не создана.";

    /// <summary>Верхняя граница отображаемой нагрузки: выше неё точное число не информативно.</summary>
    public const decimal DebtLoadCap = 9.99m;

    /// <summary>Грейд, задающий верхнюю границу ставки при автоматическом одобрении.</summary>
    public const RiskGrade MaxAutoApprovalGrade = RiskGrade.C;

    /// <summary>Доля расходов в выручке, выше которой правила скоринга rules-0.3 снижают оценку (фактор expense_ratio).</summary>
    public const decimal ExpenseRatioHigh = 0.90m;

    /// <summary>Предлагаемая меньшая сумма округляется вниз до этого шага.</summary>
    public const decimal AmountSuggestionStep = 10_000m;

    private readonly FinancingPreviewOptions _options = options ?? throw new ArgumentNullException(nameof(options));
    private readonly DecisionPolicyOptions _policy = policy ?? throw new ArgumentNullException(nameof(policy));

    /// <summary>Сравниваются только срочные продукты с аннуитетным графиком; овердрафт и кредитная линия оформляются отдельной заявкой.</summary>
    public static bool IsComparable(ProductType type) => type is ProductType.WorkingCapital or ProductType.Investment or ProductType.Express;

    public FinancingPreview Build(Company company, IReadOnlyList<CreditProduct> products, FinancingPreviewQuery query, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(company);
        ArgumentNullException.ThrowIfNull(products);
        ArgumentNullException.ThrowIfNull(query);

        var today = BankCalendar.Today(now);
        var businessAgeMonths = company.GetBusinessAgeMonths(today);

        var options = new List<FinancingOption>();
        var unsuitable = new List<UnsuitableProduct>();
        var notCompared = new List<NotComparedProduct>();
        foreach (var product in products.OrderBy(p => p.SortOrder).ThenBy(p => p.Name, StringComparer.Ordinal))
        {
            if (!IsComparable(product.Type))
            {
                notCompared.Add(new NotComparedProduct(product, NotComparedReason(product)));
                continue;
            }

            var reasons = Mismatches(product, query, businessAgeMonths).ToList();
            if (reasons.Count > 0)
            {
                unsuitable.Add(new UnsuitableProduct(product, reasons, ProductMaximumSuggestion(product, query, reasons)));
                continue;
            }

            options.Add(BuildOption(company, product, query, today));
        }

        var ordered = Rank(options);
        var comparableCount = options.Count + unsuitable.Count;
        var summary = ordered.Count > 0 ? OptionsSummary(ordered, comparableCount) : NoOptionsSummary(unsuitable, notCompared);

        return new FinancingPreview(
            now,
            today,
            company,
            businessAgeMonths,
            query,
            query.NeedByDate.DayNumber - today.DayNumber,
            summary,
            Disclaimer,
            ordered,
            notCompared,
            unsuitable);
    }

    private static PreviewNote NotComparedReason(CreditProduct product) => product.Type switch
    {
        ProductType.Overdraft => new PreviewNote("limit_product", $"«{product.Name}»: лимит к расчётному счёту, проценты начисляются только за дни использования. Платёж и переплата по аннуитетной модели не считаются; оформляется отдельной заявкой из каталога."),
        ProductType.CreditLine => new PreviewNote("limit_product", $"«{product.Name}»: возобновляемый лимит, транши выбираются по мере необходимости. Платёж и переплата по аннуитетной модели не считаются; оформляется отдельной заявкой из каталога."),
        _ => new PreviewNote("limit_product", $"«{product.Name}»: продукт без аннуитетного графика, по этой модели не сравнивается."),
    };

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

    /// <summary>Если продукту мешает только сумма, предлагается его максимум для повторного расчёта.</summary>
    private static AmountSuggestion? ProductMaximumSuggestion(CreditProduct product, FinancingPreviewQuery query, IReadOnlyList<PreviewNote> reasons)
    {
        if (reasons.Count != 1 || reasons[0].Code != "amount_above_max" || product.MaxAmount >= query.Amount)
        {
            return null;
        }

        return new AmountSuggestion(
            product.MaxAmount,
            AmountSuggestionBasis.ProductMaximum,
            $"{Money.FormatRub(product.MaxAmount)}: максимальная сумма продукта «{product.Name}». Пересчитайте с этой суммой, и продукт попадёт в подходящие варианты.");
    }

    private FinancingOption BuildOption(Company company, CreditProduct product, FinancingPreviewQuery query, DateOnly today)
    {
        var termSuggested = query.TermMonths is null;
        var (term, termReason) = query.TermMonths is { } requested ? (requested, (TermSuggestionReason?)null) : SuggestTerm(company, product, query.Amount);
        var cost = Cost(query.Amount, product.BaseRate, term);
        var load = DebtLoad(company, cost.MonthlyPayment);
        var affordability = Affordability(company, cost.MonthlyPayment, load);
        var funding = Funding(product, query, today);
        var suggestedAmount = SuggestAmount(company, product, query, term, affordability, load);

        return new FinancingOption(
            product,
            IsRecommended: false,
            query.Amount,
            term,
            termSuggested,
            cost,
            affordability,
            funding,
            suggestedAmount,
            Explanations(product, funding, term, termReason).ToList(),
            Limitations(company, product, query, cost, affordability, funding).ToList(),
            Requirements(company, product, query).ToList());
    }

    /// <summary>
    /// Самый короткий срок с комфортной нагрузкой; иначе самый короткий без штрафа в скоринге (меньше 50 %); иначе меньше 70 %;
    /// иначе максимальный срок продукта. Границы строгие, как в банде фактора debt_load.
    /// </summary>
    private (int Term, TermSuggestionReason Reason) SuggestTerm(Company company, CreditProduct product, decimal amount)
    {
        if (MonthlyProfit(company).Profit is null)
        {
            return (product.MaxTermMonths, TermSuggestionReason.MaxTermInsufficientData);
        }

        var ladder = new[]
        {
            (_options.ComfortableDebtLoad, TermSuggestionReason.Comfortable),
            (_options.ElevatedDebtLoad, TermSuggestionReason.NoPenalty),
            (_options.HighDebtLoad, TermSuggestionReason.BelowHigh),
        };
        foreach (var (threshold, reason) in ladder)
        {
            for (var term = product.MinTermMonths; term <= product.MaxTermMonths; term++)
            {
                var load = DebtLoad(company, LoanMath.MonthlyPayment(amount, product.BaseRate, term)).Load;
                if (load is { } value && value < threshold)
                {
                    return (term, reason);
                }
            }
        }

        return (product.MaxTermMonths, TermSuggestionReason.MaxTermHighLoad);
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

    /// <summary>Месячная прибыль: чистая прибыль, иначе выручка минус расходы, иначе данных недостаточно (оценка по доле выручки не выдаётся).</summary>
    private static (decimal? Profit, ProfitBasis? Basis) MonthlyProfit(Company company)
    {
        if (company.NetProfit is { } netProfit)
        {
            return (netProfit / 12m, ProfitBasis.NetProfit);
        }

        if (company.AnnualExpenses is { } expenses)
        {
            return ((company.AnnualRevenue - expenses) / 12m, ProfitBasis.RevenueMinusExpenses);
        }

        return (null, null);
    }

    /// <summary>Формула сервиса скоринга: (текущие платежи + новый платёж) / месячная прибыль. Без прибыли нагрузка не определена.</summary>
    private static (decimal? Load, decimal? Profit, ProfitBasis? Basis) DebtLoad(Company company, decimal monthlyPayment)
    {
        var (profit, basis) = MonthlyProfit(company);
        if (profit is not { } value || value <= 0)
        {
            return (null, profit, basis);
        }

        return ((company.MonthlyDebtPayments + monthlyPayment) / value, profit, basis);
    }

    private Affordability Affordability(Company company, decimal monthlyPayment, (decimal? Load, decimal? Profit, ProfitBasis? Basis) debtLoad)
    {
        var (load, profit, basis) = debtLoad;
        var level = (profit, load) switch
        {
            (null, _) => DebtLoadLevel.InsufficientData,
            (_, null) => DebtLoadLevel.High,
            (_, var value) when value < _options.ComfortableDebtLoad => DebtLoadLevel.Comfortable,
            (_, var value) when value < _options.ElevatedDebtLoad => DebtLoadLevel.Moderate,
            (_, var value) when value < _options.HighDebtLoad => DebtLoadLevel.Elevated,
            _ => DebtLoadLevel.High,
        };

        var basisNote = basis == ProfitBasis.RevenueMinusExpenses ? " Прибыль оценена как выручка минус расходы." : string.Empty;
        string explanation;
        if (profit is null)
        {
            explanation = "Нагрузку оценить нельзя: в карточке компании нет ни чистой прибыли, ни расходов за последние 12 месяцев. " +
                          "Укажите их, и расчёт обновится; при рассмотрении заявки банк оценит нагрузку по своим правилам.";
        }
        else if (load is null)
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
            explanation = $"Платёж {Money.FormatRub(monthlyPayment)} вместе с текущими платежами {Money.FormatRub(company.MonthlyDebtPayments)} составит {Percent(load.Value)} месячной прибыли {Money.FormatRub(profit.Value)}: {levelText}.{basisNote}";
        }

        return new Affordability(
            profit is { } p ? Math.Round(p, 2) : null,
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

        var path = product.RequiresCollateral
            ? "Нужен залог, заявку рассматривает кредитный менеджер. При одобрении, оформлении залога и подписании договора"
            : aboveAutoLimit
                ? $"Сумма выше лимита автоматического решения {Money.FormatRub(product.AutoApprovalLimit)}, заявку рассматривает кредитный менеджер. При одобрении и подписании договора"
                : $"Сумма в пределах лимита автоматического решения {Money.FormatRub(product.AutoApprovalLimit)}: при чистых проверках и хорошей оценке решение принимается без менеджера. При одобрении и подписании договора";
        var outcome = inTime
            ? $"до нужной даты {query.NeedByDate:dd.MM.yyyy} остаётся {RussianPlural.Count(query.NeedByDate.DayNumber - expected.DayNumber, "день", "дня", "дней")}"
            : $"это позже нужной даты {query.NeedByDate:dd.MM.yyyy}";

        return new FundingEstimate(
            manual ? FundingPath.ManualReview : FundingPath.Auto,
            businessDays,
            expected,
            inTime,
            $"{path} деньги ориентировочно {expected:dd.MM.yyyy}, {outcome}. Ориентир не является одобрением.");
    }

    /// <summary>
    /// Меньшая сумма для повторного расчёта: наименьшая из действующих границ (лимит автоматического решения, доля выручки,
    /// посильная нагрузка). Без данных о прибыли граница по нагрузке не считается; ниже минимума продукта не предлагается.
    /// </summary>
    private AmountSuggestion? SuggestAmount(Company company, CreditProduct product, FinancingPreviewQuery query, int term, Affordability affordability, (decimal? Load, decimal? Profit, ProfitBasis? Basis) debtLoad)
    {
        var candidates = new List<AmountSuggestion>();

        if (!product.RequiresCollateral && query.Amount > product.AutoApprovalLimit)
        {
            candidates.Add(new AmountSuggestion(
                product.AutoApprovalLimit,
                AmountSuggestionBasis.AutoApprovalLimit,
                $"До {Money.FormatRub(product.AutoApprovalLimit)} решение может быть принято автоматически, без менеджера. Пересчитайте с этой суммой."));
        }

        var revenueCap = Math.Max(Math.Round(company.AnnualRevenue * _policy.MaxAmountShareOfRevenue, 0), product.MinAmount);
        if (query.Amount > revenueCap)
        {
            candidates.Add(new AmountSuggestion(
                revenueCap,
                AmountSuggestionBasis.RevenueShare,
                $"{Money.FormatRub(revenueCap)}: {Percent(_policy.MaxAmountShareOfRevenue)} годовой выручки, предел суммы при одобрении с условиями (средний риск). Пересчитайте с этой суммой."));
        }

        if (affordability.Level is DebtLoadLevel.Elevated or DebtLoadLevel.High && debtLoad.Profit is { } profit && profit > 0)
        {
            var maxPayment = profit * _options.ElevatedDebtLoad - company.MonthlyDebtPayments;
            if (maxPayment > 0)
            {
                var paymentPerRuble = LoanMath.MonthlyPayment(query.Amount, product.BaseRate, term) / query.Amount;
                var affordable = Math.Floor(maxPayment / paymentPerRuble / AmountSuggestionStep) * AmountSuggestionStep;
                // Аннуитет округляется до копеек: проверяем границу на реальном платеже
                while (affordable >= product.MinAmount && (company.MonthlyDebtPayments + LoanMath.MonthlyPayment(affordable, product.BaseRate, term)) / profit >= _options.ElevatedDebtLoad)
                {
                    affordable -= AmountSuggestionStep;
                }

                if (affordable >= product.MinAmount && affordable < query.Amount)
                {
                    candidates.Add(new AmountSuggestion(
                        affordable,
                        AmountSuggestionBasis.DebtLoad,
                        $"При {Money.FormatRub(affordable)} платёж вместе с текущими обязательствами составит меньше {Percent(_options.ElevatedDebtLoad)} месячной прибыли: без штрафа за нагрузку в оценке риска. Пересчитайте с этой суммой."));
                }
            }
        }

        var best = candidates.MinBy(c => c.Amount);
        return best is null || best.Amount >= query.Amount || best.Amount < product.MinAmount ? null : best;
    }

    private static IEnumerable<PreviewNote> Explanations(CreditProduct product, FundingEstimate funding, int term, TermSuggestionReason? termReason)
    {
        yield return new PreviewNote("product_about", product.Description);

        if (!product.RequiresCollateral)
        {
            yield return new PreviewNote("no_collateral", "Без залога.");
        }

        if (funding.DecisionPath == FundingPath.Auto)
        {
            yield return new PreviewNote("auto_decision_possible", $"Сумма в пределах лимита автоматического решения {Money.FormatRub(product.AutoApprovalLimit)}: при чистых проверках и хорошей оценке решение принимается без менеджера, иначе заявку рассмотрит менеджер.");
        }

        if (termReason is { } reason)
        {
            var range = $"можно выбрать от {product.MinTermMonths} до {product.MaxTermMonths} мес.";
            yield return new PreviewNote("term_suggested", reason switch
            {
                TermSuggestionReason.Comfortable => $"Срок {term} мес.: самый короткий срок с комфортной нагрузкой и минимальной переплатой; {range}",
                TermSuggestionReason.NoPenalty => $"Срок {term} мес.: самый короткий срок без штрафа за нагрузку в оценке риска; {range}",
                TermSuggestionReason.BelowHigh => $"Срок {term} мес.: самый короткий срок, при котором нагрузка ниже высокой; {range}",
                TermSuggestionReason.MaxTermHighLoad => $"Срок {term} мес. максимальный для продукта: нагрузка остаётся высокой при любом сроке; {range}",
                _ => $"Срок {term} мес. взят максимальный для продукта (минимальный платёж), потому что нагрузку оценить нельзя; {range}",
            });
        }

        yield return new PreviewNote("data_prefilled", "Выручка, налоги и реквизиты подгружаются из кабинета автоматически, вводить их не нужно.");
        yield return new PreviewNote("checks_automatic", "После подачи банк сам запросит сведения ЕГРЮЛ, ФНС, кредитную историю и ФССП (на стенде это симуляторы); отдельных документов и согласий в кабинете не требуется.");
    }

    private IEnumerable<PreviewNote> Limitations(Company company, CreditProduct product, FinancingPreviewQuery query, OptionCost cost, Affordability affordability, FundingEstimate funding)
    {
        yield return new PreviewNote("rate_depends_on_risk", $"Ставка от {Money.FormatRate(cost.InterestRate)} до {Money.FormatRate(cost.MaxInterestRate)} годовых: точную ставку банк назначает после оценки риска.");

        if (query.Amount > product.AutoApprovalLimit)
        {
            yield return new PreviewNote("manual_review_amount", $"Сумма выше лимита автоматического решения {Money.FormatRub(product.AutoApprovalLimit)}: заявку рассматривает кредитный менеджер.");
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

        switch (affordability.Level)
        {
            case DebtLoadLevel.InsufficientData:
                yield return new PreviewNote("affordability_unknown", "Нагрузку оценить нельзя: в карточке компании нет чистой прибыли и расходов за 12 месяцев. Банк оценит её при рассмотрении заявки.");
                break;
            case DebtLoadLevel.Elevated when affordability.DebtLoad is { } elevated:
                yield return new PreviewNote("elevated_debt_load", $"Платёж вместе с текущими обязательствами составит {Percent(elevated)} месячной прибыли: оценка риска снижается, ставка может быть выше.");
                break;
            case DebtLoadLevel.High:
                yield return new PreviewNote(
                    "high_debt_load",
                    affordability.DebtLoad is { } load
                        ? $"Платёж вместе с текущими обязательствами составит {Percent(load)} месячной прибыли: возможен отказ или снижение суммы."
                        : "По данным компании прибыли нет: возможен отказ или снижение суммы.");
                break;
        }

        if (!funding.InTime)
        {
            yield return new PreviewNote("deadline_missed", $"Ориентир получения {funding.ExpectedDate:dd.MM.yyyy} позже нужной даты {query.NeedByDate:dd.MM.yyyy}.");
        }
    }

    /// <summary>Только то, что действительно потребуется: проверка полноты данных на этапе Validate и залог.</summary>
    private static IEnumerable<PreviewNote> Requirements(Company company, CreditProduct product, FinancingPreviewQuery query)
    {
        if (ValidationRules.NeedsFinancialStatements(query.Amount, product.Type) && (company.AnnualExpenses is null || company.NetProfit is null))
        {
            yield return new PreviewNote("financials_required", "Расходы и чистая прибыль за последние 12 месяцев в карточке компании: без них заявка остановится на уточнении данных.");
        }

        if (product.RequiresCollateral)
        {
            yield return new PreviewNote("collateral_documents", "Документы на предмет залога: менеджер запросит их при рассмотрении.");
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

    private static string OptionsSummary(IReadOnlyList<FinancingOption> ordered, int comparableProducts)
    {
        var best = ordered[0];
        var verb = RussianPlural.Form(ordered.Count, "Подходит", "Подходят", "Подходят");
        var count = RussianPlural.Count(ordered.Count, "вариант", "варианта", "вариантов");
        var products = RussianPlural.Count(comparableProducts, "сравниваемого продукта", "сравниваемых продуктов", "сравниваемых продуктов");
        var deadline = best.Funding.InTime ? "к нужной дате успеваем" : "это позже нужной даты";
        return $"{verb} {count} из {products}. Рекомендуем «{best.Product.Name}»: платёж {Money.FormatRub(best.Cost.MonthlyPayment)} в месяц, " +
               $"переплата {Money.FormatRub(best.Cost.TotalInterest)} за {best.TermMonths} мес., при одобрении деньги ориентировочно {best.Funding.ExpectedDate:dd.MM.yyyy}, {deadline}.";
    }

    private static string NoOptionsSummary(IReadOnlyList<UnsuitableProduct> unsuitable, IReadOnlyList<NotComparedProduct> notCompared)
    {
        var tail = notCompared.Count > 0
            ? $" Продукты с лимитом ({string.Join(", ", notCompared.Select(n => $"«{n.Product.Name}»"))}) в сравнение не входят и оформляются отдельной заявкой."
            : string.Empty;
        if (unsuitable.Count == 0)
        {
            return $"Подходящих срочных продуктов нет: в каталоге нет активных продуктов с аннуитетным графиком.{tail}";
        }

        // Одинаковые причины у разных продуктов объединяются: «…не менее 6 мес. Продукты: «Экспресс-кредит», «Оборотный кредит».»
        var groups = unsuitable
            .SelectMany(u => u.Reasons.Select(r => (r.Text, u.Product.Name)))
            .GroupBy(x => x.Text, x => x.Name, StringComparer.Ordinal)
            .Select(g => $"{g.Key} {(g.Count() == 1 ? "Продукт" : "Продукты")}: {string.Join(", ", g.Select(name => $"«{name}»"))}.");
        return $"Подходящих продуктов нет. {string.Join(" ", groups)}{tail}";
    }

    private static string Percent(decimal share) => $"{PercentValue(share)} %";

    private static string PercentValue(decimal share) => Math.Round(share * 100, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}
