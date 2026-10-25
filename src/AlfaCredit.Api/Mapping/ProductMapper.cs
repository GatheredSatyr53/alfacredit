using AlfaCredit.Api.Contracts.Financing;
using AlfaCredit.Api.Contracts.Products;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Financing;

namespace AlfaCredit.Api.Mapping;

public static class ProductMapper
{
    public static ProductDto ToDto(this CreditProduct product, PreliminaryPayment? preliminaryPayment = null) => new(
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
        product.RequiresCollateral,
        preliminaryPayment?.ToDto());

    public static PreliminaryPaymentDto ToDto(this PreliminaryPayment payment) => new(
        payment.Amount,
        payment.TermMonths,
        payment.InterestRate,
        payment.IsAvailable,
        payment.MonthlyPayment,
        payment.TotalPayment,
        payment.TotalInterest,
        payment.Notes.Select(n => new PreviewNoteDto(n.Code, n.Text)).ToArray());
}
