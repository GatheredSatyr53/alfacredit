using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Core.Features.Scoring;

/// <summary>
/// Встроенная балльная модель. Явная и объяснимая: каждому фактору соответствует вклад в баллы и фраза для клиента.
/// Описана в docs/decision-logic.md. Используется сама по себе или как запасной вариант при недоступности внешнего сервиса.
/// </summary>
public sealed class RuleBasedScoringService : IScoringService
{
    public const string ModelVersion = "rules-v1";
    public const int BaseScore = 50;

    public Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.FromResult(Score(request));
    }

    public static ScoringOutcome Score(ScoringRequest request)
    {
        var factors = new List<ScoringFactor>();
        var warnings = new List<string>();
        var company = request.Company;
        var financials = company.Financials;
        var checks = request.Checks;

        // 1. Возраст бизнеса
        var age = company.BusinessAgeMonths;
        var ageImpact = age switch { < 6 => -20, < 12 => -10, < 36 => 0, _ => 10 };
        factors.Add(new ScoringFactor(
            "business_age",
            "Возраст бизнеса",
            ageImpact,
            FormatMonths(age),
            ageImpact >= 10 ? "Компания работает больше трёх лет, это снижает риск."
            : ageImpact == 0 ? "Компания работает от года до трёх лет."
            : "Бизнес молодой, банк относится к таким заявкам осторожнее."));

        // 2. Выручка к запрошенной сумме
        var ratio = request.Application.AmountRub <= 0 ? 0m : financials.RevenueRub / request.Application.AmountRub;
        var ratioImpact = ratio switch { >= 4 => 15, >= 2 => 5, >= 1 => -5, _ => -20 };
        factors.Add(new ScoringFactor(
            "revenue_to_amount",
            "Выручка к сумме кредита",
            ratioImpact,
            $"{ratio:0.0}x",
            ratioImpact > 0 ? "Запрошенная сумма небольшая относительно годовой выручки."
            : "Запрошенная сумма велика относительно годовой выручки."));

        // 3. Долговая нагрузка: платежи по кредитам к месячной прибыли
        decimal monthlyProfit;
        if (financials.NetProfitRub is { } netProfit)
        {
            monthlyProfit = netProfit / 12m;
        }
        else
        {
            monthlyProfit = financials.RevenueRub * 0.10m / 12m;
            warnings.Add("net_profit_missing");
        }

        var totalPayments = financials.MonthlyDebtPaymentsRub + request.Application.EstimatedMonthlyPaymentRub;
        var noProfit = monthlyProfit <= 0;
        var load = noProfit ? 9.99m : totalPayments / monthlyProfit;
        var loadImpact = load switch { < 0.3m => 10, < 0.5m => 0, < 0.7m => -10, _ => -25 };
        factors.Add(new ScoringFactor(
            "debt_load",
            "Долговая нагрузка",
            loadImpact,
            noProfit ? "прибыль отсутствует" : $"{load:0.00}",
            loadImpact > 0 ? "Платежи по кредитам занимают небольшую часть прибыли."
            : loadImpact == 0 ? "Платежи по кредитам занимают около половины месячной прибыли."
            : "Платежи по кредитам с учётом нового займа занимают слишком большую часть прибыли."));

        // 4. Кредитная история
        if (checks.CreditBureau is { } bureau)
        {
            int historyImpact;
            string historyValue;
            string historyExplanation;
            if (bureau.CreditHistoryMonths == 0 && bureau.ActiveLoans == 0 && bureau.ClosedLoans == 0)
            {
                historyImpact = 0;
                historyValue = "нет истории";
                historyExplanation = "Кредитной истории пока нет, это не плюс и не минус.";
            }
            else if (bureau.HasWriteOffs || bureau.MaxOverdueDays24M > 90)
            {
                historyImpact = -30;
                historyValue = $"просрочка {bureau.MaxOverdueDays24M} дн.";
                historyExplanation = "В кредитной истории есть длительные просрочки или списания.";
            }
            else
            {
                historyImpact = bureau.MaxOverdueDays24M switch { 0 => 15, <= 30 => -5, _ => -15 };
                historyValue = bureau.MaxOverdueDays24M == 0 ? "без просрочек" : $"просрочка до {bureau.MaxOverdueDays24M} дн.";
                historyExplanation = historyImpact > 0
                    ? "Просрочек по кредитам за два года нет."
                    : "За последние два года были просрочки по кредитам.";
            }

            factors.Add(new ScoringFactor("credit_history", "Кредитная история", historyImpact, historyValue, historyExplanation));
        }
        else
        {
            warnings.Add("credit_bureau_missing");
        }

        // 5. Налоговая задолженность и блокировки
        if (checks.Fns is { } fns)
        {
            var taxImpact = fns.TaxDebtRub switch { <= 0 => 5, < 50_000 => -5, _ => -15 };
            if (fns.HasBlockedAccounts)
            {
                taxImpact -= 15;
            }

            factors.Add(new ScoringFactor(
                "tax_debt",
                "Налоговая задолженность",
                taxImpact,
                fns.TaxDebtRub <= 0 ? "нет" : Money.FormatRub(fns.TaxDebtRub),
                taxImpact > 0 ? "Задолженности перед налоговой нет."
                : fns.HasBlockedAccounts ? "Есть налоговая задолженность и заблокированные счета."
                : "Есть задолженность перед налоговой."));
        }
        else
        {
            warnings.Add("fns_missing");
        }

        // 6. Исполнительные производства
        if (checks.Fssp is { } fssp)
        {
            var fsspImpact = fssp.OpenProceedings > 0 ? -10 : 0;
            factors.Add(new ScoringFactor(
                "enforcement",
                "Исполнительные производства",
                fsspImpact,
                fssp.OpenProceedings == 0 ? "нет" : $"{fssp.OpenProceedings} на {Money.FormatRub(fssp.OpenAmountRub)}",
                fsspImpact == 0 ? "Открытых исполнительных производств нет." : "Есть открытые исполнительные производства."));
        }
        else
        {
            warnings.Add("fssp_missing");
        }

        // 7. Залог
        if (request.Application.CollateralOffered)
        {
            factors.Add(new ScoringFactor("collateral", "Обеспечение", 10, "предложено", "Предложенный залог снижает риск банка."));
        }

        var score = Math.Clamp(BaseScore + factors.Sum(f => f.Impact), 0, 100);
        var grade = RiskGrades.FromScore(score);
        var probabilityOfDefault = Math.Round(0.02m + (100 - score) / 100m * 0.30m, 3);

        var ordered = factors.OrderByDescending(f => Math.Abs(f.Impact)).ThenBy(f => f.Code).ToArray();
        return new ScoringOutcome(score, grade, probabilityOfDefault, ordered, warnings, request.MissingInputs(), ModelVersion, ScoringSource.Rules);
    }

    private static string FormatMonths(int months)
    {
        var years = months / 12;
        var rest = months % 12;
        return years switch
        {
            0 => $"{rest} мес.",
            _ when rest == 0 => $"{years} г.",
            _ => $"{years} г. {rest} мес.",
        };
    }
}
