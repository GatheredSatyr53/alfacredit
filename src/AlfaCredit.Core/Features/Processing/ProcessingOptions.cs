using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Core.Features.Processing;

public sealed class ProcessingOptions
{
    public const string SectionName = "Processing";

    /// <summary>Пауза между этапами конвейера, чтобы на демо было видно движение заявки.</summary>
    [Range(0, 60)]
    public int StageDelaySeconds { get; set; } = 2;

    [Range(1, 16)]
    public int WorkerCount { get; set; } = 2;

    [Range(50, 60_000)]
    public int PollIntervalMs { get; set; } = 500;

    [Range(1, 10)]
    public int MaxAttempts { get; set; } = 3;

    [Range(0, 600)]
    public int RetryDelaySeconds { get; set; } = 2;

    [Range(5, 3600)]
    public int OfferExpiryCheckSeconds { get; set; } = 60;

    /// <summary>Аренда задания воркером: если воркер погиб, не отметив задание, после этого срока его заберёт другой цикл.</summary>
    [Range(10, 3600)]
    public int LeaseSeconds { get; set; } = 120;

    public TimeSpan StageDelay => TimeSpan.FromSeconds(StageDelaySeconds);

    public TimeSpan Lease => TimeSpan.FromSeconds(LeaseSeconds);
}
