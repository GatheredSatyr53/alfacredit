using AlfaCredit.Core.Domain.Processing;

namespace AlfaCredit.Core.Abstractions;

public interface IProcessingJobRepository
{
    void Add(ProcessingJob job);

    /// <summary>Атомарно забирает одно готовое к выполнению задание и помечает его как выполняющееся; null, если очередь пуста.</summary>
    Task<ProcessingJob?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task<ProcessingJob?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
