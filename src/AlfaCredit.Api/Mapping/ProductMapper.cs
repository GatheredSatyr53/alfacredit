using AlfaCredit.Api.Contracts.Products;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Mapping;

public static class ProductMapper
{
    public static ProductDto ToDto(this CreditProduct product) => new(
        product.Code,
        product.Name,
        product.Description,
        product.Type,
        product.MinAmount,
        product.MaxAmount,
        product.MinTermMonths,
        product.MaxTermMonths,
        product.BaseRate,
        product.AllowedPurposes.Select(p => new LoanPurposeDto(p, p.Title())).ToArray(),
        product.MinBusinessAgeMonths,
        product.RequiresCollateral);
}
