using AlfaCredit.Api.Contracts.Applications;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Contracts;

namespace AlfaCredit.Api.Mapping;

public static class ContractMapper
{
    public static string DocumentUrl(Guid applicationId) => $"/api/applications/{applicationId}/contract/document";

    public static ContractSummaryDto ToSummaryDto(this LoanContract contract, DateTimeOffset now) => new(
        contract.Id,
        contract.Number,
        contract.Status,
        contract.Amount,
        contract.TermMonths,
        contract.InterestRate,
        contract.MonthlyPayment,
        contract.TotalPayment,
        contract.TotalInterest,
        contract.DisbursementAccount,
        contract.CreatedAt,
        contract.SigningDeadline,
        contract.SignedAt,
        contract.ToSigningDto(now),
        DocumentUrl(contract.ApplicationId));

    public static ContractDto ToDto(this LoanContract contract, CreditApplication application, DateTimeOffset now)
    {
        var company = application.Company;
        return new ContractDto(
            contract.Id,
            contract.Number,
            contract.Status,
            application.Id,
            application.Number,
            new ContractPartyDto(contract.LenderName),
            new ContractBorrowerDto(company.Id, company.Name, company.Inn, company.Ogrn, company.Address, company.HeadName),
            new ApplicationProductDto(application.Product.Code, application.Product.Name),
            application.Purpose,
            application.Purpose.Title(),
            contract.Amount,
            contract.TermMonths,
            contract.InterestRate,
            contract.MonthlyPayment,
            contract.TotalPayment,
            contract.TotalInterest,
            contract.DisbursementAccount,
            contract.CreatedAt,
            contract.SigningDeadline,
            contract.SignedAt,
            contract.SignedByUserId is { } signer ? new ContractSignerDto(signer, contract.SignedByName ?? string.Empty) : null,
            contract.SignatureMethod,
            contract.ToSigningDto(now),
            ContractSchedule.Build(application, contract).Select(r => new ScheduleRowDto(r.Number, r.DueDate, r.Payment, r.Principal, r.Interest, r.Balance)).ToArray(),
            DocumentUrl(application.Id));
    }

    public static DisbursementDto ToDto(this Disbursement disbursement) => new(
        disbursement.Id,
        disbursement.Amount,
        disbursement.Account,
        disbursement.PaymentOrderNumber,
        disbursement.Status,
        disbursement.RequestedAt,
        disbursement.CompletedAt,
        disbursement.Error);

    private static ContractSigningDto ToSigningDto(this LoanContract contract, DateTimeOffset now)
    {
        var codeActive = contract.ConfirmationCode is not null && contract.CodeExpiresAt is { } expires && expires > now;
        return new ContractSigningDto(
            codeActive ? contract.CodeRequestedAt : null,
            codeActive ? contract.CodeExpiresAt : null,
            codeActive ? contract.AttemptsLeft : contract.CodeMaxAttempts,
            contract.CanRequestCode(now));
    }
}
