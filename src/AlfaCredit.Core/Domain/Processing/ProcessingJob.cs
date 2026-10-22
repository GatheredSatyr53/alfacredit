namespace AlfaCredit.Core.Domain.Processing;

/// <summary>Задание конвейера, хранится в базе: переживает рестарт и позволяет запускать несколько воркеров.</summary>
public sealed class ProcessingJob
{
    public const int DefaultMaxAttempts = 3;
    public const int ErrorMaxLength = 2000;
    public const string InterruptedError = "Выполнение прервано перезапуском сервиса, задание возвращено в очередь.";

    private ProcessingJob()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public ProcessingStage Stage { get; private set; }

    public ProcessingJobStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public int MaxAttempts { get; private set; }

    /// <summary>Не раньше этого момента воркер возьмёт задание: так реализована задержка между этапами.</summary>
    public DateTimeOffset NextRunAt { get; private set; }

    /// <summary>Пока аренда не истекла, задание считается выполняющимся. Истёкшая аренда означает, что воркер погиб, и задание можно забрать снова.</summary>
    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public string? LastError { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool CanRetry => Attempts < MaxAttempts;

    public static ProcessingJob Create(Guid applicationId, ProcessingStage stage, DateTimeOffset now, TimeSpan delay, int maxAttempts = DefaultMaxAttempts) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            Stage = stage,
            Status = ProcessingJobStatus.Pending,
            Attempts = 0,
            MaxAttempts = Math.Max(1, maxAttempts),
            NextRunAt = now + (delay < TimeSpan.Zero ? TimeSpan.Zero : delay),
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void MarkRunning(DateTimeOffset now, TimeSpan lease)
    {
        Status = ProcessingJobStatus.Running;
        Attempts++;
        LeaseExpiresAt = now + (lease < TimeSpan.Zero ? TimeSpan.Zero : lease);
        UpdatedAt = now;
    }

    public bool IsLeaseExpired(DateTimeOffset now) =>
        Status == ProcessingJobStatus.Running && LeaseExpiresAt is { } lease && lease <= now;

    public void MarkDone(DateTimeOffset now)
    {
        Status = ProcessingJobStatus.Done;
        LastError = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    /// <summary>Возвращает true, если задание поставлено на повтор, и false, если попытки исчерпаны.</summary>
    public bool MarkFailed(string error, DateTimeOffset now, TimeSpan retryDelay)
    {
        LastError = error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error;
        LeaseExpiresAt = null;
        UpdatedAt = now;

        if (CanRetry)
        {
            Status = ProcessingJobStatus.Pending;
            NextRunAt = now + retryDelay;
            return true;
        }

        Status = ProcessingJobStatus.Failed;
        return false;
    }

    /// <summary>Возвращает прерванное задание в очередь, не засчитывая прерывание как неудачную попытку: обработка продолжается после перезапуска.</summary>
    public void Requeue(DateTimeOffset now)
    {
        Status = ProcessingJobStatus.Pending;
        Attempts = Math.Max(0, Attempts - 1);
        NextRunAt = now;
        LeaseExpiresAt = null;
        LastError = InterruptedError;
        UpdatedAt = now;
    }
}
