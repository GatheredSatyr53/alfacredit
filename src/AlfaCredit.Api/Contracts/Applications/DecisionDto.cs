using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Кредитное решение по заявке.</summary>
/// <param name="Type">Approved, ApprovedWithConditions, Rejected или ManualReview.</param>
/// <param name="IsFinal">Окончательное решение; для ManualReview false.</param>
/// <param name="DecidedBy">Auto или Manager.</param>
/// <param name="RecommendedType">Рекомендация автоматики, если решение передано менеджеру.</param>
/// <param name="ApprovedAmount">Одобренная сумма, ₽.</param>
/// <param name="ApprovedTermMonths">Одобренный срок, мес.</param>
/// <param name="InterestRate">Годовая ставка, %.</param>
/// <param name="MonthlyPayment">Ежемесячный платёж, ₽.</param>
/// <param name="Conditions">Изменённые условия, текстом.</param>
/// <param name="Reasons">Причины решения с объяснением для клиента.</param>
/// <param name="DecidedAt">Когда принято (UTC).</param>
public sealed record DecisionDto(
    DecisionType Type,
    bool IsFinal,
    DecisionActor DecidedBy,
    DecisionType? RecommendedType,
    decimal? ApprovedAmount,
    int? ApprovedTermMonths,
    decimal? InterestRate,
    decimal? MonthlyPayment,
    string? Conditions,
    IReadOnlyList<DecisionReasonDto> Reasons,
    DateTimeOffset DecidedAt);

/// <summary>Причина решения.</summary>
public sealed record DecisionReasonDto(string Code, string Title, string Explanation);

/// <summary>Предложение банка по одобренной заявке.</summary>
/// <param name="Id">Идентификатор предложения.</param>
/// <param name="Amount">Сумма, ₽.</param>
/// <param name="TermMonths">Срок, мес.</param>
/// <param name="InterestRate">Годовая ставка, %.</param>
/// <param name="MonthlyPayment">Ежемесячный платёж, ₽.</param>
/// <param name="ExpiresAt">До какого момента действует (UTC).</param>
/// <param name="Response">Pending, Accepted, Declined или Expired.</param>
/// <param name="RespondedAt">Когда клиент ответил (UTC).</param>
public sealed record OfferDto(
    Guid Id,
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    decimal MonthlyPayment,
    DateTimeOffset ExpiresAt,
    OfferResponse Response,
    DateTimeOffset? RespondedAt);

/// <summary>Результат скоринга. Виден только сотрудникам банка.</summary>
public sealed record ScoringDto(
    int Score,
    RiskGrade Grade,
    decimal? ProbabilityOfDefault,
    IReadOnlyList<ScoringFactorDto> Factors,
    IReadOnlyList<string> Warnings,
    string ModelVersion,
    ScoringSource Source,
    DateTimeOffset ComputedAt);

/// <summary>Фактор скоринга.</summary>
public sealed record ScoringFactorDto(string Code, string Title, int Impact, string Value, string ClientExplanation);

/// <summary>Результат внешней проверки. Виден только сотрудникам банка.</summary>
public sealed record ExternalCheckDto(
    CheckSource Source,
    string SourceTitle,
    CheckStatus Status,
    string Summary,
    IReadOnlyList<string> RiskFlags,
    int Attempts,
    DateTimeOffset CheckedAt,
    string? Error);
