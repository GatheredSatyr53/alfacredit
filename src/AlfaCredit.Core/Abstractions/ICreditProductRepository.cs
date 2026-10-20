using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Abstractions;

public interface ICreditProductRepository
{
    Task<IReadOnlyList<CreditProduct>> ListActiveAsync(CancellationToken cancellationToken);

    /// <param name="code">Код продукта в любом регистре.</param>
    Task<CreditProduct?> FindByCodeAsync(string code, CancellationToken cancellationToken);
}
