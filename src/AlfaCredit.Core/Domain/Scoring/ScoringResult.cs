using System.Text.Json;

namespace AlfaCredit.Core.Domain.Scoring;

/// <summary>Грейд риска. Границы по баллу описаны в <see cref="RiskGrades"/>.</summary>
public enum RiskGrade
{
    A = 1,
    B = 2,
    C = 3,
    D = 4,
    E = 5,
}

public static class RiskGrades
{
    public static RiskGrade FromScore(int score) => score switch
    {
        >= 80 => RiskGrade.A,
        >= 65 => RiskGrade.B,
        >= 50 => RiskGrade.C,
        >= 35 => RiskGrade.D,
        _ => RiskGrade.E,
    };

    /// <summary>Надбавка к базовой ставке продукта в процентных пунктах.</summary>
    public static decimal RatePremium(RiskGrade grade) => grade switch
    {
        RiskGrade.A => 0m,
        RiskGrade.B => 1.5m,
        RiskGrade.C => 3m,
        RiskGrade.D => 5m,
        _ => 8m,
    };
}

public enum ScoringSource
{
    /// <summary>Встроенная балльная модель.</summary>
    Rules = 1,

    /// <summary>Внешний сервис скоринга.</summary>
    External = 2,

    /// <summary>Внешний сервис был недоступен, использована встроенная модель.</summary>
    RulesFallback = 3,
}

/// <summary>Фактор скоринга с объяснением для клиента.</summary>
/// <param name="Code">Код фактора, например debt_load.</param>
/// <param name="Title">Название для карточки менеджера.</param>
/// <param name="Impact">Вклад в балл со знаком.</param>
/// <param name="Value">Значение показателя строкой.</param>
/// <param name="ClientExplanation">Фраза для клиента простым языком.</param>
public sealed record ScoringFactor(string Code, string Title, int Impact, string Value, string ClientExplanation);

public sealed class ScoringResult
{
    public const int ModelVersionMaxLength = 64;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ScoringResult()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>0..100, чем выше, тем ниже риск.</summary>
    public int Score { get; private set; }

    public RiskGrade Grade { get; private set; }

    public decimal? ProbabilityOfDefault { get; private set; }

    public string FactorsJson { get; private set; } = "[]";

    public List<string> Warnings { get; private set; } = [];

    public string ModelVersion { get; private set; } = string.Empty;

    public ScoringSource Source { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public IReadOnlyList<ScoringFactor> Factors =>
        JsonSerializer.Deserialize<List<ScoringFactor>>(FactorsJson, JsonOptions) ?? [];

    public static ScoringResult Create(
        Guid applicationId,
        int score,
        RiskGrade grade,
        decimal? probabilityOfDefault,
        IEnumerable<ScoringFactor> factors,
        IEnumerable<string> warnings,
        string modelVersion,
        ScoringSource source,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Score = Math.Clamp(score, 0, 100),
            Grade = grade,
            ProbabilityOfDefault = probabilityOfDefault is { } pd ? Math.Clamp(pd, 0m, 1m) : null,
            FactorsJson = JsonSerializer.Serialize(factors.ToList(), JsonOptions),
            Warnings = warnings.Distinct().ToList(),
            ModelVersion = modelVersion.Length > ModelVersionMaxLength ? modelVersion[..ModelVersionMaxLength] : modelVersion,
            Source = source,
            ComputedAt = now,
        };
}
