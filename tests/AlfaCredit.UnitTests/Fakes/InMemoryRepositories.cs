using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.UnitTests.Fakes;

internal sealed class InMemoryCompanyRepository(params IEnumerable<Company> companies) : ICompanyRepository
{
    private readonly List<Company> _companies = [.. companies];

    public Task<Company?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_companies.FirstOrDefault(c => c.Id == id));

    public Task<IReadOnlyList<Company>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Company>>(_companies.Where(c => c.HasMember(userId)).ToArray());

    public Task<IReadOnlyList<Company>> ListAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Company>>(_companies.ToArray());

    public Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(_companies.Any(c => c.Id == companyId && c.HasMember(userId)));

    public Task<IReadOnlyList<Guid>> GetMemberCompanyIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Guid>>(_companies.Where(c => c.HasMember(userId)).Select(c => c.Id).ToArray());
}

internal sealed class InMemoryProductRepository(params IEnumerable<CreditProduct> products) : ICreditProductRepository
{
    private readonly List<CreditProduct> _products = [.. products];

    public Task<IReadOnlyList<CreditProduct>> ListActiveAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreditProduct>>(_products.Where(p => p.IsActive).ToArray());

    public Task<CreditProduct?> FindByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(_products.FirstOrDefault(p => p.Code == CreditProduct.NormalizeCode(code)));
}

internal sealed class InMemoryApplicationRepository : ICreditApplicationRepository
{
    public List<CreditApplication> Items { get; } = [];

    public void Add(CreditApplication application) => Items.Add(application);

    public Task<CreditApplication?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(a => a.Id == id));

    public Task<CompanyApplicationHistory> GetCompanyHistoryAsync(Guid companyId, Guid excludeApplicationId, CancellationToken cancellationToken)
    {
        var others = Items.Where(a => a.CompanyId == companyId && a.Id != excludeApplicationId).ToArray();
        return Task.FromResult(new CompanyApplicationHistory(
            others.Length,
            others.Count(a => a.Status == ApplicationStatus.Rejected),
            others.Count(a => a.Status == ApplicationStatus.Disbursed)));
    }

    public Task<IReadOnlyList<CreditApplication>> ListWithExpiredOffersAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CreditApplication>>(Items
            .Where(a => a.Status is ApplicationStatus.Approved or ApplicationStatus.ApprovedWithConditions && a.CurrentOffer is { } o && o.IsExpired(now))
            .ToArray());

    public Task<PagedResult<CreditApplication>> ListAsync(ApplicationFilter filter, CancellationToken cancellationToken)
    {
        var query = Items.AsEnumerable();
        if (filter.CompanyIds is not null)
        {
            query = query.Where(a => filter.CompanyIds.Contains(a.CompanyId));
        }

        if (filter.Status is { } status)
        {
            query = query.Where(a => a.Status == status);
        }

        var all = query.OrderByDescending(a => a.CreatedAt).ToArray();
        var page = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToArray();
        return Task.FromResult(new PagedResult<CreditApplication>(page, filter.Page, filter.PageSize, all.Length));
    }
}

internal sealed class CountingUnitOfWork : IUnitOfWork
{
    public int SaveCalls { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCalls++;
        return Task.FromResult(1);
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
