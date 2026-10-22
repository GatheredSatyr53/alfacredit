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

/// <summary>Результат скоринга независимо от того, кто его посчитал: встроенные правила или внешний сервис.</summary>
/// <param name="Score">0..100, чем выше, тем ниже риск.</param>
/// <param name="Grade">Грейд риска A..E.</param>
/// <param name="ProbabilityOfDefault">Оценка вероятности дефолта 0..1, если модель её даёт.</param>
/// <param name="Factors">Главные факторы по убыванию модуля вклада.</param>
/// <param name="Warnings">Предупреждения модели: чем заменены отсутствующие входы, сбои и т.п.</param>
/// <param name="MissingData">Каких входных данных не хватало: пути полей запроса, например company.financials.net_profit_rub.</param>
/// <param name="ModelVersion">Версия модели для воспроизводимости.</param>
/// <param name="Source">Кто посчитал: Rules, External или RulesFallback.</param>
public sealed record ScoringOutcome(
    int Score,
    RiskGrade Grade,
    decimal? ProbabilityOfDefault,
    IReadOnlyList<ScoringFactor> Factors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> MissingData,
    string ModelVersion,
    ScoringSource Source);

/// <summary>Сохранённый результат скоринга: балл, грейд, факторы, предупреждения, пропуски в данных, версия модели и время расчёта.</summary>
public sealed class ScoringResult
{
    public const int ModelVersionMaxLength = 64;
    public const int ListMaxLength = 512;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private ScoringResult()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>Идентификатор запроса к сервису скоринга: по нему расчёт находится в логах сервиса.</summary>
    public Guid? RequestId { get; private set; }

    /// <summary>0..100, чем выше, тем ниже риск.</summary>
    public int Score { get; private set; }

    public RiskGrade Grade { get; private set; }

    public decimal? ProbabilityOfDefault { get; private set; }

    public string FactorsJson { get; private set; } = "[]";

    public List<string> Warnings { get; private set; } = [];

    /// <summary>Каких входных данных не хватало при расчёте: пути полей запроса к скорингу.</summary>
    public List<string> MissingData { get; private set; } = [];

    public string ModelVersion { get; private set; } = string.Empty;

    public ScoringSource Source { get; private set; }

    /// <summary>Длительность расчёта в миллисекундах с учётом повторов и ожидания внешнего сервиса.</summary>
    public int DurationMs { get; private set; }

    public DateTimeOffset ComputedAt { get; private set; }

    public IReadOnlyList<ScoringFactor> Factors =>
        JsonSerializer.Deserialize<List<ScoringFactor>>(FactorsJson, JsonOptions) ?? [];

    public static ScoringResult Create(Guid applicationId, Guid? requestId, ScoringOutcome outcome, int durationMs, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var modelVersion = string.IsNullOrWhiteSpace(outcome.ModelVersion) ? "unknown" : outcome.ModelVersion.Trim();
        return new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            RequestId = requestId,
            Score = Math.Clamp(outcome.Score, 0, 100),
            Grade = outcome.Grade,
            ProbabilityOfDefault = outcome.ProbabilityOfDefault is { } pd ? Math.Clamp(pd, 0m, 1m) : null,
            FactorsJson = JsonSerializer.Serialize(outcome.Factors.ToList(), JsonOptions),
            Warnings = Fit(outcome.Warnings),
            MissingData = Fit(outcome.MissingData),
            ModelVersion = modelVersion.Length > ModelVersionMaxLength ? modelVersion[..ModelVersionMaxLength] : modelVersion,
            Source = outcome.Source,
            DurationMs = Math.Max(0, durationMs),
            ComputedAt = now,
        };
    }

    /// <summary>Коды хранятся строкой через запятую: без пустых и дублей, запятые внутри кода заменяются, общая длина ограничена колонкой.</summary>
    private static List<string> Fit(IEnumerable<string> items)
    {
        var result = new List<string>();
        var length = 0;
        foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim().Replace(',', ';')).Distinct(StringComparer.Ordinal))
        {
            var extra = result.Count == 0 ? item.Length : item.Length + 1;
            if (length + extra > ListMaxLength)
            {
                break;
            }

            result.Add(item);
            length += extra;
        }

        return result;
    }
}
