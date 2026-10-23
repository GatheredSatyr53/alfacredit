using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
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
            .Include(a => a.Checks.OrderBy(c => c.CheckedAt))
            .Include(a => a.ScoringResults.OrderBy(s => s.ComputedAt))
            .Include(a => a.Decisions.OrderBy(d => d.DecidedAt))
            .Include(a => a.Offers.OrderBy(o => o.CreatedAt))
            .Include(a => a.Contracts.OrderBy(c => c.CreatedAt))
            .Include(a => a.Disbursements.OrderBy(d => d.RequestedAt))
            .AsSplitQuery()
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

    public async Task<CompanyApplicationHistory> GetCompanyHistoryAsync(Guid companyId, Guid excludeApplicationId, CancellationToken cancellationToken)
    {
        var query = db.CreditApplications.AsNoTracking().Where(a => a.CompanyId == companyId && a.Id != excludeApplicationId);
        var total = await query.CountAsync(cancellationToken);
        var rejected = await query.CountAsync(a => a.Status == ApplicationStatus.Rejected, cancellationToken);
        var disbursed = await query.CountAsync(a => a.Status == ApplicationStatus.Disbursed, cancellationToken);
        return new CompanyApplicationHistory(total, rejected, disbursed);
    }

    public async Task<IReadOnlyList<CreditApplication>> ListWithExpiredOffersAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.CreditApplications
            .Include(a => a.Company).ThenInclude(c => c.Members)
            .Include(a => a.Product)
            .Include(a => a.Offers)
            .Where(a => (a.Status == ApplicationStatus.Approved || a.Status == ApplicationStatus.ApprovedWithConditions)
                && a.Offers.Any(o => o.Response == OfferResponse.Pending && o.ExpiresAt <= now))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreditApplication>> ListWithExpiredContractsAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.CreditApplications
            .Include(a => a.Company).ThenInclude(c => c.Members)
            .Include(a => a.Product)
            .Include(a => a.Contracts)
            .Where(a => a.Status == ApplicationStatus.ContractSigning
                && a.Contracts.Any(c => c.Status == ContractStatus.AwaitingSignature && c.SigningDeadline <= now))
            .ToListAsync(cancellationToken);
}
