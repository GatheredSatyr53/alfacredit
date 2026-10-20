using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Infrastructure.Scoring;

public enum ScoringMode
{
    /// <summary>Встроенная балльная модель.</summary>
    Rules = 1,

    /// <summary>Внешний сервис скоринга по docs/scoring-contract.md с откатом на встроенную модель.</summary>
    External = 2,
}

public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";

    public ScoringMode Mode { get; set; } = ScoringMode.Rules;

    /// <summary>Адрес внешнего сервиса, например http://scoring:8000.</summary>
    public string? BaseUrl { get; set; }

    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 5;

    [Range(0, 5)]
    public int Retries { get; set; } = 2;
}
