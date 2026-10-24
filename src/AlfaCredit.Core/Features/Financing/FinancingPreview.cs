using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>Уровень кредитной нагрузки: доля платежей по кредитам в месячной прибыли. Банды те же, что у фактора debt_load правил скоринга rules-0.3.</summary>
public enum DebtLoadLevel
{
    /// <summary>Меньше 30 %: в скоринге +10 баллов.</summary>
    Comfortable = 1,

    /// <summary>30–50 %: заметная, но посильная, в скоринге 0 баллов.</summary>
    Moderate = 2,

    /// <summary>50–70 %: оценка риска снижается (−10 баллов).</summary>
    Elevated = 3,

    /// <summary>70 % и больше или прибыли нет: максимальный штраф (−25 баллов), возможен отказ или снижение суммы.</summary>
    High = 4,

    /// <summary>В карточке компании нет ни чистой прибыли, ни расходов: нагрузку оценить нельзя, оценка не выдаётся.</summary>
    InsufficientData = 5,
}

/// <summary>Откуда взята месячная прибыль для расчёта нагрузки.</summary>
public enum ProfitBasis
{
    /// <summary>Чистая прибыль за 12 месяцев из карточки компании.</summary>
    NetProfit = 1,

    /// <summary>Выручка минус расходы: чистая прибыль не указана.</summary>
    RevenueMinusExpenses = 2,
}

/// <summary>Как будет принято решение по заявке на этих условиях.</summary>
public enum FundingPath
{
    /// <summary>Сумма в лимите автоматического решения, залог не нужен: при чистых проверках и хорошей оценке решение без менеджера.</summary>
    Auto = 1,

    /// <summary>Заявку в любом случае рассматривает менеджер: крупная сумма или залог.</summary>
    ManualReview = 2,
}

/// <summary>Из чего следует предлагаемая меньшая сумма.</summary>
public enum AmountSuggestionBasis
{
    /// <summary>Максимальная сумма продукта.</summary>
    ProductMaximum = 1,

    /// <summary>Лимит автоматического решения продукта.</summary>
    AutoApprovalLimit = 2,

    /// <summary>Доля годовой выручки, до которой политика снижает сумму при среднем риске.</summary>
    RevenueShare = 3,

    /// <summary>Сумма, при которой платёж укладывается в нагрузку без штрафа в оценке риска.</summary>
    DebtLoad = 4,
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

/// <summary>«Потяну ли платёж»: нагрузка по формуле сервиса скоринга или признак, что данных недостаточно.</summary>
/// <param name="MonthlyProfit">Месячная прибыль для расчёта; null, если данных недостаточно.</param>
/// <param name="DebtLoad">(текущие платежи + новый платёж) / месячная прибыль, не больше 9.99; null, если прибыли нет или данных недостаточно.</param>
/// <param name="ProfitBasis">Откуда прибыль; null, если данных недостаточно.</param>
public sealed record Affordability(
    decimal? MonthlyProfit,
    decimal CurrentDebtPayments,
    decimal? DebtLoad,
    DebtLoadLevel Level,
    ProfitBasis? ProfitBasis,
    string Explanation);

/// <summary>«Успею ли к дате»: ориентир получения денег в рабочих днях от даты расчёта при положительном решении.</summary>
public sealed record FundingEstimate(FundingPath DecisionPath, int BusinessDays, DateOnly ExpectedDate, bool InTime, string Explanation);

/// <summary>Меньшая сумма для повторного расчёта: число, основание и объяснение.</summary>
public sealed record AmountSuggestion(decimal Amount, AmountSuggestionBasis Basis, string Text);

/// <summary>Почему и как подобран срок, если клиент его не указал.</summary>
public enum TermSuggestionReason
{
    /// <summary>Самый короткий срок с комфортной нагрузкой.</summary>
    Comfortable = 1,

    /// <summary>Самый короткий срок без штрафа за нагрузку в оценке риска.</summary>
    NoPenalty = 2,

    /// <summary>Самый короткий срок с нагрузкой ниже высокой.</summary>
    BelowHigh = 3,

    /// <summary>Максимальный срок продукта: нагрузка высокая при любом сроке.</summary>
    MaxTermHighLoad = 4,

    /// <summary>Максимальный срок продукта: нагрузку оценить нельзя.</summary>
    MaxTermInsufficientData = 5,
}

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
    AmountSuggestion? SuggestedAmount,
    IReadOnlyList<PreviewNote> Explanations,
    IReadOnlyList<PreviewNote> Limitations,
    IReadOnlyList<PreviewNote> Requirements);

/// <summary>Продукт, который не подходит, и почему; если мешает только сумма, предлагается максимальная сумма продукта.</summary>
public sealed record UnsuitableProduct(CreditProduct Product, IReadOnlyList<PreviewNote> Reasons, AmountSuggestion? SuggestedAmount);

/// <summary>Продукт с лимитом (овердрафт, кредитная линия): по аннуитетной модели не сравнивается, оформляется отдельной заявкой.</summary>
public sealed record NotComparedProduct(CreditProduct Product, PreviewNote Reason);

/// <summary>Результат предварительного подбора. Не сохраняется.</summary>
/// <param name="CalculationDate">Календарная дата расчёта по Москве: от неё считаются возраст бизнеса, дни до нужной даты и ориентиры.</param>
public sealed record FinancingPreview(
    DateTimeOffset CalculatedAt,
    DateOnly CalculationDate,
    Company Company,
    int BusinessAgeMonths,
    FinancingPreviewQuery Request,
    int DaysUntilNeeded,
    string Summary,
    string Disclaimer,
    IReadOnlyList<FinancingOption> Options,
    IReadOnlyList<NotComparedProduct> NotCompared,
    IReadOnlyList<UnsuitableProduct> Unsuitable);
