using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Abstractions;

/// <param name="CompanyIds">Ограничить выборку компаниями; null означает без ограничения.</param>
public sealed record ApplicationFilter(IReadOnlyCollection<Guid>? CompanyIds, ApplicationStatus? Status, int Page, int PageSize);

/// <summary>Сколько заявок компания подавала раньше: признак для скоринга.</summary>
public sealed record CompanyApplicationHistory(int PreviousApplications, int PreviousRejections, int PreviousLoansDisbursed);

public interface ICreditApplicationRepository
{
    void Add(CreditApplication application);

    Task<CompanyApplicationHistory> GetCompanyHistoryAsync(Guid companyId, Guid excludeApplicationId, CancellationToken cancellationToken);

    /// <summary>Одобренные заявки, у которых истёк срок предложения, а клиент не ответил.</summary>
    Task<IReadOnlyList<CreditApplication>> ListWithExpiredOffersAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Заявка с компанией, продуктом и историей статусов; отслеживается контекстом для последующих изменений.</summary>
    Task<CreditApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<PagedResult<CreditApplication>> ListAsync(ApplicationFilter filter, CancellationToken cancellationToken);
}
