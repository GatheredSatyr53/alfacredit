using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlfaCredit.Core.Domain.Checks;

public enum CheckStatus
{
    Succeeded = 1,
    Failed = 2,
}

/// <summary>Результат обращения к внешнему источнику по заявке. Сырой ответ хранится как JSON для аудита.</summary>
public sealed class ExternalCheck
{
    public const int SummaryMaxLength = 500;
    public const int ErrorMaxLength = 1000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    private ExternalCheck()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public CheckSource Source { get; private set; }

    public CheckStatus Status { get; private set; }

    public string? PayloadJson { get; private set; }

    /// <summary>Краткий вывод для карточки менеджера, по-русски.</summary>
    public string Summary { get; private set; } = string.Empty;

    /// <summary>Коды сигналов риска, например tax_debt, overdue_90.</summary>
    public List<string> RiskFlags { get; private set; } = [];

    public int Attempts { get; private set; }

    public string? Error { get; private set; }

    public DateTimeOffset CheckedAt { get; private set; }

    public static ExternalCheck Succeeded<T>(Guid applicationId, CheckSource source, T payload, string summary, IEnumerable<string> riskFlags, int attempts, DateTimeOffset now)
        where T : class =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Source = source,
            Status = CheckStatus.Succeeded,
            PayloadJson = JsonSerializer.Serialize(payload, JsonOptions),
            Summary = Truncate(summary, SummaryMaxLength),
            RiskFlags = riskFlags.Distinct().ToList(),
            Attempts = attempts,
            CheckedAt = now,
        };

    public static ExternalCheck Failed(Guid applicationId, CheckSource source, string error, int attempts, DateTimeOffset now) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Source = source,
            Status = CheckStatus.Failed,
            Summary = $"Источник недоступен после {attempts} попыток.",
            Error = Truncate(error, ErrorMaxLength),
            Attempts = attempts,
            CheckedAt = now,
        };

    public T? Payload<T>()
        where T : class =>
        Status == CheckStatus.Succeeded && PayloadJson is not null
            ? JsonSerializer.Deserialize<T>(PayloadJson, JsonOptions)
            : null;

    /// <summary>Собирает типизированный снимок из последних успешных проверок по каждому источнику.</summary>
    public static ChecksSnapshot ToSnapshot(IEnumerable<ExternalCheck> checks)
    {
        var latest = checks
            .Where(c => c.Status == CheckStatus.Succeeded)
            .GroupBy(c => c.Source)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CheckedAt).First());

        return new ChecksSnapshot(
            latest.TryGetValue(CheckSource.Egrul, out var egrul) ? egrul.Payload<EgrulResult>() : null,
            latest.TryGetValue(CheckSource.Fns, out var fns) ? fns.Payload<FnsResult>() : null,
            latest.TryGetValue(CheckSource.CreditBureau, out var bureau) ? bureau.Payload<CreditBureauResult>() : null,
            latest.TryGetValue(CheckSource.Fssp, out var fssp) ? fssp.Payload<FsspResult>() : null);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
