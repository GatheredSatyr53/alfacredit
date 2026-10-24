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
    public void Offer_carries_total_payment_and_overpayment_for_its_own_terms()
    {
        var application = ApprovedApplication();
        var offer = application.CurrentOffer.ShouldNotBeNull();

        offer.TotalPayment.ShouldBe(LoanMath.Totals(1_000_000, 19.5m, 12).TotalPayment);
        offer.TotalInterest.ShouldBe(LoanMath.Totals(1_000_000, 19.5m, 12).TotalInterest);
        offer.TotalPayment.ShouldBe(offer.Amount + offer.TotalInterest);
        offer.TotalInterest.ShouldBeInRange(100_000m, 115_000m);
    }

    [Fact]
    public void Offer_totals_follow_the_changed_conditions_not_the_requested_ones()
    {
        var offer = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 1_500_000, 12, 19.5m, 138_593.06m, TestData.Now, TimeSpan.FromDays(7));

        offer.TotalPayment.ShouldBe(1_663_116.74m);
        offer.TotalInterest.ShouldBe(163_116.74m);

        var reduced = Offer.Create(Guid.NewGuid(), Guid.NewGuid(), 2_400_000, 24, 22.5m, 125_100.96m, TestData.Now, TimeSpan.FromDays(7));

        reduced.TotalPayment.ShouldBe(3_002_423.02m);
        reduced.TotalInterest.ShouldBe(602_423.02m);
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
