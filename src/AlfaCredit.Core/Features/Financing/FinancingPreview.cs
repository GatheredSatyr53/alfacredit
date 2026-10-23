using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>Уровень кредитной нагрузки: доля платежей по кредитам в месячной прибыли.</summary>
public enum DebtLoadLevel
{
    /// <summary>Меньше порога комфортной нагрузки (30 %).</summary>
    Comfortable = 1,

    /// <summary>Между порогами (30–70 %): заметная, но посильная.</summary>
    Moderate = 2,

    /// <summary>Выше верхнего порога (70 %) или прибыли нет.</summary>
    High = 3,
}

/// <summary>Откуда взята месячная прибыль для расчёта нагрузки.</summary>
public enum ProfitBasis
{
    /// <summary>Чистая прибыль за 12 месяцев из карточки компании.</summary>
    NetProfit = 1,

    /// <summary>Выручка минус расходы: чистая прибыль не указана.</summary>
    RevenueMinusExpenses = 2,

    /// <summary>Ни прибыли, ни расходов: принято 10 % выручки.</summary>
    RevenueEstimate = 3,
}

/// <summary>Как будет принято решение по заявке на этих условиях.</summary>
public enum FundingPath
{
    /// <summary>Сумма в лимите автоодобрения, залог не нужен.</summary>
    Auto = 1,

    /// <summary>Решение принимает менеджер: крупная сумма или залог.</summary>
    ManualReview = 2,
}

/// <summary>Объяснение, ограничение, требование или причина несоответствия: код для интерфейса и текст для клиента.</summary>
public sealed record PreviewNote(string Code, string Text);

/// <summary>Стоимость варианта при базовой ставке и при верхней границе ставки автоматического одобрения.</summary>
public sealed record OptionCost(
    decimal InterestRate,
    decimal MaxInterestRate,
    decimal MonthlyPayment,
    decimal MaxMonthlyPayment,
    decimal TotalPayment,
    decimal TotalInterest,
    decimal MaxTotalInterest);

/// <summary>«Потяну ли платёж»: нагрузка по формуле сервиса скоринга.</summary>
/// <param name="DebtLoad">(текущие платежи + новый платёж) / месячная прибыль, не больше 9.99; null, если прибыли нет.</param>
public sealed record Affordability(
    decimal MonthlyProfit,
    decimal CurrentDebtPayments,
    decimal? DebtLoad,
    DebtLoadLevel Level,
    ProfitBasis ProfitBasis,
    string Explanation);

/// <summary>«Успею ли к дате»: ориентир получения денег в рабочих днях от даты расчёта.</summary>
public sealed record FundingEstimate(FundingPath DecisionPath, int BusinessDays, DateOnly ExpectedDate, bool InTime, string Explanation);

/// <summary>Подходящий продукт с расчётом.</summary>
public sealed record FinancingOption(
    CreditProduct Product,
    bool IsRecommended,
    decimal Amount,
    int TermMonths,
    bool TermSuggested,
    OptionCost Cost,
    Affordability Affordability,
    FundingEstimate Funding,
    IReadOnlyList<PreviewNote> Explanations,
    IReadOnlyList<PreviewNote> Limitations,
    IReadOnlyList<PreviewNote> Requirements);

/// <summary>Продукт, который не подходит, и почему.</summary>
public sealed record UnsuitableProduct(CreditProduct Product, IReadOnlyList<PreviewNote> Reasons);

/// <summary>Результат предварительного подбора. Не сохраняется.</summary>
public sealed record FinancingPreview(
    DateTimeOffset CalculatedAt,
    Company Company,
    int BusinessAgeMonths,
    FinancingPreviewQuery Request,
    int DaysUntilNeeded,
    string Summary,
    string Disclaimer,
    IReadOnlyList<FinancingOption> Options,
    IReadOnlyList<UnsuitableProduct> Unsuitable);
