using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.UnitTests.Applications;

internal static class TestData
{
    public static readonly DateTimeOffset Now = new(2026, 10, 20, 10, 0, 0, TimeSpan.Zero);

    public static Company Company(
        string inn = "7701234567",
        DateOnly? registeredAt = null,
        Guid? memberUserId = null,
        decimal annualRevenue = 48_000_000,
        decimal? netProfit = 7_200_000,
        decimal monthlyDebtPayments = 150_000,
        decimal? annualExpenses = null)
    {
        var company = Core.Domain.Companies.Company.Create(
            inn,
            "1157746123456",
            "ООО «Ромашка»",
            LegalForm.Ooo,
            "47.11",
            registeredAt ?? new DateOnly(2019, 3, 12),
            "Москва, ул. Садовая, д. 12",
            "Иванова Мария Сергеевна",
            TaxRegime.Osno,
            annualRevenue,
            // Без явных расходов они выводятся из прибыли; у seed-компаний расходы заданы отдельно (ООО «Вектор»: 160 млн при прибыли 16 млн)
            annualExpenses ?? annualRevenue - (netProfit ?? 0),
            netProfit,
            monthlyDebtPayments,
            24,
            Now);

        if (memberUserId is { } userId)
        {
            company.AddMember(userId, CompanyMemberRole.Director, Now);
        }

        return company;
    }

    public static CreditProduct Product(
        string code = "working-capital",
        decimal minAmount = 300_000,
        decimal maxAmount = 10_000_000,
        int minTermMonths = 3,
        int maxTermMonths = 24,
        IEnumerable<LoanPurpose>? allowedPurposes = null,
        int minBusinessAgeMonths = 12,
        decimal autoApprovalLimit = 5_000_000,
        bool requiresCollateral = false,
        decimal baseRate = 19.5m) =>
        CreditProduct.Create(
            code,
            "Оборотный кредит",
            "Пополнение оборотных средств",
            ProductType.WorkingCapital,
            minAmount,
            maxAmount,
            minTermMonths,
            maxTermMonths,
            baseRate,
            allowedPurposes ?? [LoanPurpose.WorkingCapital, LoanPurpose.Other],
            minBusinessAgeMonths,
            requiresCollateral,
            autoApprovalLimit,
            sortOrder: 1);
}
