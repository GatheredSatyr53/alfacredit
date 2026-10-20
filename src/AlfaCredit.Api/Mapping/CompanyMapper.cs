using AlfaCredit.Api.Contracts.Companies;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Companies;

namespace AlfaCredit.Api.Mapping;

public static class CompanyMapper
{
    public static CompanyDto ToDto(this Company company, Actor actor, DateOnly today) => new(
        company.Id,
        company.Inn,
        company.Ogrn,
        company.Name,
        company.LegalForm,
        company.Okved,
        company.RegisteredAt,
        company.GetBusinessAgeMonths(today),
        company.Address,
        company.HeadName,
        company.TaxRegime,
        company.AnnualRevenue,
        company.AnnualExpenses,
        company.NetProfit,
        company.MonthlyDebtPayments,
        company.EmployeesCount,
        actor.IsBankEmployee ? null : company.Members.FirstOrDefault(m => m.UserId == actor.UserId)?.Role);
}
