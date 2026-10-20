using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class CreditApplicationRepository(AppDbContext db) : ICreditApplicationRepository
{
    public void Add(CreditApplication application) => db.CreditApplications.Add(application);

    public Task<CreditApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.CreditApplications
            .Include(a => a.Company).ThenInclude(c => c.Members)
            .Include(a => a.Product)
            .Include(a => a.History.OrderBy(h => h.OccurredAt))
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<PagedResult<CreditApplication>> ListAsync(ApplicationFilter filter, CancellationToken cancellationToken)
    {
        var query = db.CreditApplications
            .AsNoTracking()
            .Include(a => a.Company)
            .Include(a => a.Product)
            .AsQueryable();

        if (filter.CompanyIds is not null)
        {
            var companyIds = filter.CompanyIds.ToArray();
            query = query.Where(a => companyIds.Contains(a.CompanyId));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CreditApplication>(items, filter.Page, filter.PageSize, totalCount);
    }
}
