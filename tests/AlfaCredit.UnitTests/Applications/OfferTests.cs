using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Products;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class OfferTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Accepting_offer_moves_application_to_contract_signing()
    {
        var application = ApprovedApplication();

        var offer = application.AcceptOffer(UserId, TestData.Now.AddHours(1));

        offer.Response.ShouldBe(OfferResponse.Accepted);
        application.Status.ShouldBe(ApplicationStatus.ContractSigning);
        Should.Throw<ConflictException>(() => application.AcceptOffer(UserId, TestData.Now.AddHours(2)));
    }

    [Fact]
    public void Term_loan_offer_carries_total_payment_and_overpayment_for_its_own_terms()
    {
        var application = ApprovedApplication();
        var offer = application.CurrentOffer.ShouldNotBeNull();

        var totals = offer.Totals(application.Product.Type).ShouldNotBeNull();

        totals.ShouldBe(LoanMath.Totals(1_000_000, 19.5m, 12));
        totals.TotalPayment.ShouldBe(1_108_744.51m);
        totals.TotalInterest.ShouldBe(108_744.51m);
        totals.TotalPayment.ShouldBe(offer.Amount + totals.TotalInterest);
    }

    [Fact]
    public void Offer_totals_follow_the_changed_conditions_not_the_requested_ones()
    {
        var offer = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 1_500_000, 12, 19.5m, 138_593.06m, TestData.Now, TimeSpan.FromDays(7));

        offer.Totals(ProductType.WorkingCapital).ShouldBe(new LoanTotals(1_663_116.74m, 163_116.74m));

        var reduced = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 2_400_000, 24, 22.5m, 125_100.96m, TestData.Now, TimeSpan.FromDays(7));

        reduced.Totals(ProductType.WorkingCapital).ShouldBe(new LoanTotals(3_002_423.02m, 602_423.02m));
    }

    [Theory]
    [InlineData(ProductType.WorkingCapital)]
    [InlineData(ProductType.Express)]
    [InlineData(ProductType.Investment)]
    public void Term_loans_get_annuity_totals(ProductType type)
    {
        var offer = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 1_000_000, 12, 21m, 93_114.82m, TestData.Now, TimeSpan.FromDays(7));

        offer.Totals(type).ShouldBe(LoanMath.Totals(1_000_000, 21m, 12));
    }

    [Theory]
    [InlineData(ProductType.Overdraft)]
    [InlineData(ProductType.CreditLine)]
    public void Limit_products_have_no_totals_because_interest_depends_on_usage(ProductType type)
    {
        var offer = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 1_000_000, 12, 21m, 93_114.82m, TestData.Now, TimeSpan.FromDays(7));

        offer.Totals(type).ShouldBeNull();
    }

    [Fact]
    public void Only_term_loans_have_an_annuity_schedule()
    {
        // Новый тип продукта по умолчанию получает null-итоги; тест заставит решить явно
        Enum.GetValues<ProductType>().Where(t => t.HasAnnuitySchedule())
            .ShouldBe([ProductType.WorkingCapital, ProductType.Investment, ProductType.Express], ignoreOrder: true);
    }

    [Fact]
    public void Declining_offer_closes_application()
    {
        var application = ApprovedApplication();

        application.DeclineOffer(UserId, "Дорого", TestData.Now.AddHours(1));

        application.Status.ShouldBe(ApplicationStatus.OfferDeclined);
        application.CurrentOffer!.Response.ShouldBe(OfferResponse.Declined);
        application.History[^1].Comment.ShouldBe("Дорого");
    }

    [Fact]
    public void Expired_offer_cannot_be_accepted_and_is_closed_by_expiry_check()
    {
        var application = ApprovedApplication();
        var later = TestData.Now.AddDays(8);

        Should.Throw<ConflictException>(() => application.AcceptOffer(UserId, later));
        application.ExpireOfferIfNeeded(later).ShouldBeTrue();
        application.Status.ShouldBe(ApplicationStatus.OfferDeclined);
        application.CurrentOffer!.Response.ShouldBe(OfferResponse.Expired);
        application.ExpireOfferIfNeeded(later).ShouldBeFalse();
    }

    private static CreditApplication ApprovedApplication()
    {
        var application = CreditApplication.Create(TestData.Company(memberUserId: UserId), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);
        application.BeginValidation(TestData.Now);
        application.BeginEnrichment(TestData.Now);
        application.BeginScoring(TestData.Now);
        application.BeginDeciding(TestData.Now);
        var decision = Decision.Create(application.Id, DecisionType.Approved, DecisionActor.Auto, null, null, 1_000_000, 12, 19.5m, 92_634m, null, [], TestData.Now);
        application.ApplyDecision(decision, TimeSpan.FromDays(7), TestData.Now);
        return application;
    }
}
