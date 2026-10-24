using AlfaCredit.Api.Contracts.Financing;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Financing;

namespace AlfaCredit.Api.Mapping;

public static class FinancingMapper
{
    public static FinancingPreviewDto ToDto(this FinancingPreview preview) => new(
        preview.CalculatedAt,
        preview.CalculationDate,
        new PreviewCompanyDto(preview.Company.Id, preview.Company.Name, preview.Company.Inn, preview.BusinessAgeMonths),
        new PreviewRequestDto(
            preview.Request.Amount,
            preview.Request.Purpose,
            preview.Request.Purpose.Title(),
            preview.Request.NeedByDate,
            preview.DaysUntilNeeded,
            preview.Request.TermMonths),
        preview.Summary,
        preview.Disclaimer,
        preview.Options.Select(o => o.ToDto(preview)).ToArray(),
        preview.NotCompared.Select(n => new NotComparedProductDto(n.Product.Code, n.Product.Name, n.Product.Type, ToDto(n.Reason))).ToArray(),
        preview.Unsuitable.Select(u => new UnsuitableProductDto(u.Product.Code, u.Product.Name, u.Reasons.Select(ToDto).ToArray(), u.SuggestedAmount?.ToDto())).ToArray());

    private static FinancingOptionDto ToDto(this FinancingOption option, FinancingPreview preview) => new(
        option.Product.Code,
        option.Product.Name,
        option.Product.Type,
        option.IsRecommended,
        option.Amount,
        option.TermMonths,
        option.TermSuggested,
        new TermRangeDto(option.Product.MinTermMonths, option.Product.MaxTermMonths),
        new OptionCostDto(
            option.Cost.InterestRate,
            option.Cost.MaxInterestRate,
            option.Cost.MonthlyPayment,
            option.Cost.MaxMonthlyPayment,
            option.Cost.TotalPayment,
            option.Cost.TotalInterest,
            option.Cost.MaxTotalInterest),
        new AffordabilityDto(
            option.Affordability.MonthlyProfit,
            option.Affordability.CurrentDebtPayments,
            option.Affordability.DebtLoad,
            option.Affordability.Level,
            option.Affordability.ProfitBasis,
            option.Affordability.Explanation),
        new FundingEstimateDto(
            option.Funding.DecisionPath,
            option.Funding.BusinessDays,
            option.Funding.ExpectedDate,
            option.Funding.InTime,
            option.Funding.Explanation),
        option.SuggestedAmount?.ToDto(),
        option.Explanations.Select(ToDto).ToArray(),
        option.Limitations.Select(ToDto).ToArray(),
        option.Requirements.Select(ToDto).ToArray(),
        new ApplyWithDto(preview.Company.Id, option.Product.Code, option.Amount, option.TermMonths, preview.Request.Purpose, preview.Request.NeedByDate));

    private static AmountSuggestionDto ToDto(this AmountSuggestion suggestion) => new(suggestion.Amount, suggestion.Basis, suggestion.Text);

    private static PreviewNoteDto ToDto(PreviewNote note) => new(note.Code, note.Text);
}
