using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Abstractions;

/// <param name="CompanyIds">Ограничить выборку компаниями; null означает без ограничения.</param>
public sealed record ApplicationFilter(IReadOnlyCollection<Guid>? CompanyIds, ApplicationStatus? Status, int Page, int PageSize);

public interface ICreditApplicationRepository
{
    void Add(CreditApplication application);

    /// <summary>Заявка с компанией, продуктом и историей статусов; отслеживается контекстом для последующих изменений.</summary>
    Task<CreditApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<CreditApplication>> ListAsync(ApplicationFilter filter, CancellationToken cancellationToken);
}
