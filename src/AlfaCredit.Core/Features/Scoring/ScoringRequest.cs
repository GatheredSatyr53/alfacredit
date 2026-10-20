using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Scoring;

/// <summary>Запрос к сервису скоринга. Структура повторяет docs/scoring-contract.md; в JSON поля в snake_case.</summary>
public sealed record ScoringRequest(Guid RequestId, ScoringApplication Application, ScoringCompany Company, ScoringChecks Checks);

public sealed record ScoringApplication(
    Guid Id,
    string Number,
    DateTimeOffset CreatedAt,
    decimal AmountRub,
    int TermMonths,
    LoanPurpose Purpose,
    decimal EstimatedMonthlyPaymentRub,
    ScoringProduct Product,
    bool CollateralOffered);

public sealed record ScoringProduct(string Code, ProductType Type, decimal BaseRatePercent, bool RequiresCollateral, decimal AutoApprovalLimitRub);

public sealed record ScoringCompany(
    string Inn,
    LegalForm LegalForm,
    string Okved,
    string RegionCode,
    DateOnly RegisteredAt,
    int BusinessAgeMonths,
    TaxRegime TaxRegime,
    int EmployeesCount,
    ScoringFinancials Financials,
    ScoringHistory History);

public sealed record ScoringFinancials(int PeriodMonths, decimal RevenueRub, decimal? ExpensesRub, decimal? NetProfitRub, decimal MonthlyDebtPaymentsRub);

public sealed record ScoringHistory(int PreviousApplications, int PreviousRejections, int PreviousLoansDisbursed);

public sealed record ScoringChecks(EgrulResult? Egrul, FnsResult? Fns, CreditBureauResult? CreditBureau, FsspResult? Fssp)
{
    public static ScoringChecks From(ChecksSnapshot snapshot) => new(snapshot.Egrul, snapshot.Fns, snapshot.CreditBureau, snapshot.Fssp);
}
