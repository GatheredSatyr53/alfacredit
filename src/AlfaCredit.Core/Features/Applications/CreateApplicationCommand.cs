using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Applications;

public sealed record CreateApplicationCommand(
    Guid CompanyId,
    string ProductCode,
    decimal Amount,
    int TermMonths,
    LoanPurpose Purpose,
    string? PurposeDescription);
