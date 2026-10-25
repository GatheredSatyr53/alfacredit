using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Infrastructure.Scoring;

public enum ScoringMode
{
    /// <summary>Встроенная балльная модель.</summary>
    Rules = 1,

    /// <summary>Внешний сервис скоринга по docs/scoring-contract.md с откатом на встроенную модель.</summary>
    External = 2,
}

/// <summary>Подключение сервиса скоринга. Значения по умолчанию соответствуют docs/scoring-contract.md: 5 с таймаут, 2 повтора через 1 и 2 с.</summary>
public sealed class ScoringOptions
{
    public const string SectionName = "Scoring";
    public const string DefaultBaseUrl = "http://scoring:8000";

    public ScoringMode Mode { get; set; } = ScoringMode.Rules;

    /// <summary>One version for remote inference and the embedded fallback. Legacy rules remain explicit.</summary>
    [RegularExpression(@"^(rules-0\.3|logreg-synthetic-0\.1)$")]
    public string ModelVersion { get; set; } = "rules-0.3";

    /// <summary>Optional override of the bundled logreg JSON artifact.</summary>
    public string? ArtifactPath { get; set; }

    /// <summary>Адрес внешнего сервиса, например http://scoring:8000.</summary>
    public string? BaseUrl { get; set; }

    /// <summary>Таймаут одного запроса.</summary>
    [Range(1, 120)]
    public int TimeoutSeconds { get; set; } = 5;

    /// <summary>Сколько раз повторить запрос после первой неудачи (5xx, таймаут, обрыв связи).</summary>
    [Range(0, 5)]
    public int Retries { get; set; } = 2;

    /// <summary>Пауза перед повтором; умножается на номер попытки: 1 с, затем 2 с.</summary>
    [Range(0, 60_000)]
    public int RetryDelayMs { get; set; } = 1000;

    public Uri ResolveBaseAddress()
    {
        var baseUrl = string.IsNullOrWhiteSpace(BaseUrl) ? DefaultBaseUrl : BaseUrl.Trim();
        return new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
    }
}
