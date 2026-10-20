using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class CreditProductRepository(AppDbContext db) : ICreditProductRepository
{
    public async Task<IReadOnlyList<CreditProduct>> ListActiveAsync(CancellationToken cancellationToken) =>
        await db.CreditProducts
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<CreditProduct?> FindByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = CreditProduct.NormalizeCode(code);
        return db.CreditProducts.FirstOrDefaultAsync(p => p.Code == normalized, cancellationToken);
    }
}
