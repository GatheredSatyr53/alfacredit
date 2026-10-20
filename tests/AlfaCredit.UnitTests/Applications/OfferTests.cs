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
