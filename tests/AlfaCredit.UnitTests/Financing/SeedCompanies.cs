using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Infrastructure.Persistence.Seeding;

namespace AlfaCredit.UnitTests.Financing;

/// <summary>Демо-компании из seed backend в виде доменных объектов: те же данные, что видит жюри и сервис скоринга.</summary>
internal static class SeedCompanies
{
    public static Company ByLogin(string ownerLogin, Guid? memberUserId = null)
    {
        var demo = SeedData.Companies.Single(c => c.OwnerLogin == ownerLogin);
        var company = Company.Create(
            demo.Inn,
            demo.Ogrn,
            demo.Name,
            demo.LegalForm,
            demo.Okved,
            demo.RegisteredAt,
            demo.Address,
            demo.HeadName,
            demo.TaxRegime,
            demo.AnnualRevenue,
            demo.AnnualExpenses,
            demo.NetProfit,
            demo.MonthlyDebtPayments,
            demo.EmployeesCount,
            new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));

        if (memberUserId is { } userId)
        {
            company.AddMember(userId, demo.OwnerRole, new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero));
        }

        return company;
    }
}
