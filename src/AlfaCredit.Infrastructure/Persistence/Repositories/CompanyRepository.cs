using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class CompanyRepository(AppDbContext db) : ICompanyRepository
{
    public Task<Company?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Companies
            .Include(c => c.Members)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Company>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.Companies
            .AsNoTracking()
            .Include(c => c.Members)
            .Where(c => c.Members.Any(m => m.UserId == userId))
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Company>> ListAllAsync(CancellationToken cancellationToken) =>
        await db.Companies
            .AsNoTracking()
            .Include(c => c.Members)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken) =>
        db.CompanyMembers.AnyAsync(m => m.CompanyId == companyId && m.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<Guid>> GetMemberCompanyIdsAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.CompanyMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.CompanyId)
            .Distinct()
            .ToListAsync(cancellationToken);
}
