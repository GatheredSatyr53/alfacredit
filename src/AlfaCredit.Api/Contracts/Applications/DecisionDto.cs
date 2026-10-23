using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Кредитное решение по заявке.</summary>
/// <param name="Type">Approved, ApprovedWithConditions, Rejected или ManualReview.</param>
/// <param name="IsFinal">Окончательное решение; для ManualReview false.</param>
/// <param name="DecidedBy">Auto или Manager.</param>
/// <param name="IsSimulated">Ответ менеджера смоделирован демо-режимом (DemoManager); настоящий сотрудник заявку не рассматривал.</param>
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
    bool IsSimulated,
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
/// <param name="Score">Балл 0..100, чем выше, тем ниже риск.</param>
/// <param name="Grade">Грейд риска A..E.</param>
/// <param name="ProbabilityOfDefault">Вероятность дефолта 0..1, если модель её даёт.</param>
/// <param name="Factors">Главные факторы с вкладом в балл и объяснением.</param>
/// <param name="Warnings">Предупреждения модели, например net_profit_missing или external_scoring_unavailable.</param>
/// <param name="MissingData">Каких входных данных не хватало: пути полей запроса к скорингу, например checks.credit_bureau.</param>
/// <param name="ModelVersion">Версия модели, которая посчитала результат.</param>
/// <param name="Source">Rules: встроенная модель; External: сервис скоринга; RulesFallback: сервис был недоступен, посчитала встроенная модель.</param>
/// <param name="RequestId">Идентификатор запроса к сервису скоринга.</param>
/// <param name="DurationMs">Длительность расчёта, мс.</param>
/// <param name="ComputedAt">Когда посчитано (UTC).</param>
public sealed record ScoringDto(
    int Score,
    RiskGrade Grade,
    decimal? ProbabilityOfDefault,
    IReadOnlyList<ScoringFactorDto> Factors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingData,
    string ModelVersion,
    ScoringSource Source,
    Guid? RequestId,
    int DurationMs,
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
