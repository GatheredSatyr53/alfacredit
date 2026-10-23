using System.Text.Json;

namespace AlfaCredit.Core.Domain.Decisions;

public enum DecisionType
{
    Approved = 1,
    ApprovedWithConditions = 2,
    Rejected = 3,

    /// <summary>Автоматика не приняла решение, заявка передана менеджеру с рекомендацией.</summary>
    ManualReview = 4,
}

public enum DecisionActor
{
    Auto = 1,
    Manager = 2,
}

/// <summary>Причина решения с объяснением для клиента.</summary>
public sealed record DecisionReason(string Code, string Title, string ClientExplanation);

/// <summary>Кредитное решение: автоматическое или менеджера. Для ручного рассмотрения хранит рекомендацию автоматики.</summary>
public sealed class Decision
{
    public const int ConditionsMaxLength = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private Decision()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public DecisionType Type { get; private set; }

    public DecisionActor DecidedBy { get; private set; }

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>Что рекомендовала автоматика, когда решение отдано менеджеру.</summary>
    public DecisionType? RecommendedType { get; private set; }

    public decimal? ApprovedAmount { get; private set; }

    public int? ApprovedTermMonths { get; private set; }

    /// <summary>Годовая ставка, %.</summary>
    public decimal? InterestRate { get; private set; }

    public decimal? MonthlyPayment { get; private set; }

    public string? Conditions { get; private set; }

    public string ReasonsJson { get; private set; } = "[]";

    /// <summary>Окончательное решение: одобрение, одобрение с условиями или отказ.</summary>
    public bool IsFinal { get; private set; }

    /// <summary>Ответ менеджера смоделирован демо-режимом, настоящий сотрудник заявку не рассматривал.</summary>
    public bool IsSimulated { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    public IReadOnlyList<DecisionReason> Reasons =>
        JsonSerializer.Deserialize<List<DecisionReason>>(ReasonsJson, JsonOptions) ?? [];

    public static Decision Create(
        Guid applicationId,
        DecisionType type,
        DecisionActor decidedBy,
        Guid? decidedByUserId,
        DecisionType? recommendedType,
        decimal? approvedAmount,
        int? approvedTermMonths,
        decimal? interestRate,
        decimal? monthlyPayment,
        string? conditions,
        IEnumerable<DecisionReason> reasons,
        DateTimeOffset now,
        bool isSimulated = false) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Type = type,
            DecidedBy = decidedBy,
            DecidedByUserId = decidedByUserId,
            RecommendedType = recommendedType,
            ApprovedAmount = approvedAmount,
            ApprovedTermMonths = approvedTermMonths,
            InterestRate = interestRate,
            MonthlyPayment = monthlyPayment,
            Conditions = conditions is { Length: > ConditionsMaxLength } ? conditions[..ConditionsMaxLength] : conditions,
            ReasonsJson = JsonSerializer.Serialize(reasons.ToList(), JsonOptions),
            IsFinal = type != DecisionType.ManualReview,
            IsSimulated = isSimulated,
            DecidedAt = now,
        };
}
