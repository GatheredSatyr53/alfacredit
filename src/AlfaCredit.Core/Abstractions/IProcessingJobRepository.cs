using AlfaCredit.Core.Domain.Processing;

namespace AlfaCredit.Core.Abstractions;

public interface IProcessingJobRepository
{
    void Add(ProcessingJob job);

    /// <summary>
    /// Атомарно забирает одно готовое задание (или выполняющееся задание с истёкшей арендой) и помечает его выполняющимся
    /// на срок аренды; null, если очередь пуста.
    /// </summary>
    Task<ProcessingJob?> ClaimNextAsync(DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken);

    /// <summary>Возвращает в очередь задания, оставшиеся выполняющимися после остановки сервиса. Вызывается при старте воркера; возвращает их число.</summary>
    Task<int> RequeueInterruptedAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProcessingJob?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
