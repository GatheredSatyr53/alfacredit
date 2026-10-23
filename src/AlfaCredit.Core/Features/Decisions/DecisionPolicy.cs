using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Core.Features.Decisions;

/// <summary>Что автоматика решила по заявке; окончательное решение остаётся за менеджером, если тип ManualReview.</summary>
public sealed record DecisionOutcome(
    DecisionType Type,
    DecisionType? RecommendedType,
    decimal? Amount,
    int? TermMonths,
    decimal? InterestRate,
    decimal? MonthlyPayment,
    string? Conditions,
    IReadOnlyList<DecisionReason> Reasons);

/// <summary>Кредитная политика: стоп-факторы, грейды, лимиты продукта. Описана в docs/decision-logic.md.</summary>
public sealed class DecisionPolicy(DecisionPolicyOptions options)
{
    public DecisionOutcome Decide(CreditApplication application, ChecksSnapshot checks, ScoringResult scoring)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(scoring);

        var product = application.Product;
        var company = application.Company;

        // 1. Стоп-факторы: отказ независимо от балла
        var hardStops = HardStops(checks).ToList();
        if (hardStops.Count > 0)
        {
            return new DecisionOutcome(DecisionType.Rejected, null, null, null, null, null, null, hardStops);
        }

        // 2. Без критичных источников автоматика решение не принимает
        var missing = checks.MissingSources().ToList();
        if (missing.Count > 0)
        {
            var titles = string.Join(", ", missing.Select(m => m.Title()));
            return ManualReview(
                RecommendedByGrade(scoring.Grade),
                new DecisionReason("checks_missing", $"Не получены данные: {titles}", "Банку нужно дополнительно проверить сведения о компании, менеджер займётся этим."));
        }

        var baseReasons = new List<DecisionReason>
        {
            new("risk_grade", $"Грейд риска {scoring.Grade}, {scoring.Score} баллов", GradeExplanation(scoring.Grade)),
        };
        baseReasons.AddRange(scoring.Factors.Take(3).Select(f => new DecisionReason(FactorReasonCode(f.Code), f.Title, f.ClientExplanation)));

        switch (scoring.Grade)
        {
            case RiskGrade.E:
                baseReasons.Insert(0, new DecisionReason("grade_e", "Высокий уровень риска по результатам оценки", "Оценка показала высокий риск, банк не может предоставить финансирование."));
                return new DecisionOutcome(DecisionType.Rejected, null, null, null, null, null, null, baseReasons);

            case RiskGrade.D:
                return ManualReview(DecisionType.ApprovedWithConditions, new DecisionReason("grade_d", "Повышенный риск, требуется оценка менеджера", "Менеджер изучит заявку и, возможно, запросит дополнительные документы."), baseReasons);
        }

        // 3. Продукты с залогом и крупные суммы решает менеджер
        if (product.RequiresCollateral)
        {
            return ManualReview(RecommendedByGrade(scoring.Grade), new DecisionReason("collateral_required", "Продукт требует оформления залога", "Менеджер согласует с вами обеспечение по кредиту."), baseReasons);
        }

        if (application.Amount > product.AutoApprovalLimit)
        {
            return ManualReview(RecommendedByGrade(scoring.Grade), new DecisionReason("amount_above_auto_limit", $"Сумма выше лимита автоодобрения {Money.FormatRub(product.AutoApprovalLimit)}", "Крупные суммы подтверждает кредитный менеджер."), baseReasons);
        }

        // 4. Одобрение
        var rate = product.BaseRate + RiskGrades.RatePremium(scoring.Grade);
        if (scoring.Grade is RiskGrade.A or RiskGrade.B)
        {
            var payment = LoanMath.MonthlyPayment(application.Amount, rate, application.TermMonths);
            return new DecisionOutcome(DecisionType.Approved, null, application.Amount, application.TermMonths, rate, payment, null, baseReasons);
        }

        // Грейд C: сумма ограничивается долей выручки, ставка выше базовой
        var cappedAmount = Math.Min(application.Amount, Math.Round(company.AnnualRevenue * options.MaxAmountShareOfRevenue, 0));
        cappedAmount = Math.Max(cappedAmount, product.MinAmount);
        var conditions = new List<string>();
        if (cappedAmount < application.Amount)
        {
            conditions.Add($"сумма снижена до {Money.FormatRub(cappedAmount)} ({options.MaxAmountShareOfRevenue:P0} годовой выручки)");
            baseReasons.Insert(1, new DecisionReason("amount_reduced", "Сумма снижена", $"Банк готов выдать {Money.FormatRub(cappedAmount)}: это {options.MaxAmountShareOfRevenue:P0} вашей годовой выручки, комфортный уровень нагрузки."));
        }

        conditions.Add($"ставка {rate:0.##} % годовых с учётом надбавки за риск");
        var cappedPayment = LoanMath.MonthlyPayment(cappedAmount, rate, application.TermMonths);

        return new DecisionOutcome(
            DecisionType.ApprovedWithConditions,
            null,
            cappedAmount,
            application.TermMonths,
            rate,
            cappedPayment,
            string.Join("; ", conditions),
            baseReasons);
    }

    private IEnumerable<DecisionReason> HardStops(ChecksSnapshot checks)
    {
        if (checks.Egrul is { } egrul)
        {
            if (egrul.Status != EgrulStatus.Active)
            {
                yield return new DecisionReason("company_inactive", "Компания не действует по данным ЕГРЮЛ", "По данным реестра компания не ведёт деятельность или ликвидируется.");
            }

            if (egrul.HeadDisqualified)
            {
                yield return new DecisionReason("head_disqualified", "Руководитель дисквалифицирован", "Руководитель компании дисквалифицирован, банк не может выдать кредит.");
            }
        }

        if (checks.Fns is { } fns)
        {
            if (fns.TaxDebtRub >= options.TaxDebtHardStopRub)
            {
                yield return new DecisionReason("tax_debt", $"Налоговая задолженность {Money.FormatRub(fns.TaxDebtRub)}", $"Задолженность перед налоговой превышает {Money.FormatRub(options.TaxDebtHardStopRub)}. Погасите её и подайте заявку снова.");
            }

            if (fns.HasBlockedAccounts)
            {
                yield return new DecisionReason("accounts_blocked", "Счета заблокированы ФНС", "Пока действует блокировка счетов, выдача кредита невозможна.");
            }
        }

        if (checks.CreditBureau is { } bureau && (bureau.MaxOverdueDays24M > options.OverdueDaysHardStop || bureau.HasWriteOffs))
        {
            yield return new DecisionReason("overdue_90", $"Просрочка более {options.OverdueDaysHardStop} дней за последние 24 месяца", "В кредитной истории есть длительная просрочка, банк не может одобрить новый кредит.");
        }

        if (checks.Fssp is { } fssp && fssp.OpenAmountRub > options.FsspHardStopRub)
        {
            yield return new DecisionReason("enforcement_debt", $"Исполнительные производства на {Money.FormatRub(fssp.OpenAmountRub)}", $"Сумма открытых исполнительных производств превышает {Money.FormatRub(options.FsspHardStopRub)}.");
        }
    }

    /// <summary>Код причины по фактору скоринга: factor_debt_load. Коды внешнего сервиса уже с префиксом не удваиваются.</summary>
    public static string FactorReasonCode(string factorCode) =>
        factorCode.StartsWith("factor_", StringComparison.Ordinal) ? factorCode : $"factor_{factorCode}";

    private static DecisionOutcome ManualReview(DecisionType recommended, DecisionReason reason, IEnumerable<DecisionReason>? more = null) =>
        new(DecisionType.ManualReview, recommended, null, null, null, null, null, [reason, .. more ?? []]);

    private static DecisionType RecommendedByGrade(RiskGrade grade) => grade switch
    {
        RiskGrade.A or RiskGrade.B => DecisionType.Approved,
        RiskGrade.C or RiskGrade.D => DecisionType.ApprovedWithConditions,
        _ => DecisionType.Rejected,
    };

    private static string GradeExplanation(RiskGrade grade) => grade switch
    {
        RiskGrade.A => "Низкий уровень риска: финансовое положение устойчивое.",
        RiskGrade.B => "Уровень риска ниже среднего.",
        RiskGrade.C => "Средний уровень риска: банк готов кредитовать на скорректированных условиях.",
        RiskGrade.D => "Повышенный уровень риска.",
        _ => "Высокий уровень риска.",
    };
}
