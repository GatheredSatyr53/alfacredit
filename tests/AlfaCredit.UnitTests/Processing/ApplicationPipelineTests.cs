using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Processing;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.UnitTests.Applications;
using AlfaCredit.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class ApplicationPipelineTests
{
    private readonly Guid _clientId = Guid.NewGuid();
    private readonly User _manager = User.Create("manager", "hash", "Алексей Петров", UserRole.Manager, TestData.Now);
    private readonly InMemoryApplicationRepository _applications = new();
    private readonly InMemoryJobRepository _jobs = new();
    private readonly InMemoryFeedRepository _feed = new();
    private readonly FakeSources _sources = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(TestData.Now);

    [Fact]
    public async Task Clean_application_runs_through_all_stages_to_approval_with_offer()
    {
        var application = Submit(amount: 1_500_000, termMonths: 12);

        await RunPipelineAsync(application);

        application.Status.ShouldBe(ApplicationStatus.Approved);
        application.History.Select(h => h.ToStatus).ShouldBe(
        [
            ApplicationStatus.Submitted, ApplicationStatus.Validating, ApplicationStatus.Enriching,
            ApplicationStatus.Scoring, ApplicationStatus.Deciding, ApplicationStatus.Approved,
        ]);
        application.Checks.Count.ShouldBe(4);
        application.Checks.ShouldAllBe(c => c.Status == CheckStatus.Succeeded);
        application.LatestScoring!.Grade.ShouldBe(Core.Domain.Scoring.RiskGrade.A);
        application.FinalDecision!.Type.ShouldBe(DecisionType.Approved);
        application.CurrentOffer.ShouldNotBeNull();
        application.CurrentOffer!.Amount.ShouldBe(1_500_000);
        application.CurrentOffer.ExpiresAt.ShouldBe(TestData.Now.AddDays(7));
        application.AvailableActions(Core.Common.ActorType.Client).ShouldBe(["acceptOffer", "declineOffer"], ignoreOrder: true);
        _jobs.Items.ShouldAllBe(j => j.Status == ProcessingJobStatus.Done);
        _feed.Items.Where(f => f.UserId == _clientId).Select(f => f.Type).ShouldContain(FeedItemType.OfferReady);
        _notifier.Notifications.Select(n => n.Status).ShouldContain(ApplicationStatus.Approved);
    }

    [Fact]
    public async Task Hard_stop_profile_is_rejected_with_reasons()
    {
        _sources.Fns = new FnsResult(700_000, true, null);
        _sources.Bureau = new CreditBureauResult(48, 4, 1, 9_000_000, 600_000, 95, 3, false);
        _sources.Fssp = new FsspResult(2, 1_400_000);
        var application = Submit(amount: 1_000_000, termMonths: 12);

        await RunPipelineAsync(application);

        application.Status.ShouldBe(ApplicationStatus.Rejected);
        application.FinalDecision!.Reasons.Select(r => r.Code).ShouldContain("tax_debt");
        application.CurrentOffer.ShouldBeNull();
        _feed.Items.Where(f => f.UserId == _clientId).Select(f => f.Type).ShouldContain(FeedItemType.Rejected);
    }

    [Fact]
    public async Task Transient_source_failures_are_retried()
    {
        _sources.FailuresLeft[CheckSource.Fns] = 2;
        var application = Submit(amount: 1_500_000, termMonths: 12);

        await RunPipelineAsync(application);

        _sources.Calls[CheckSource.Fns].ShouldBe(3);
        application.Checks.First(c => c.Source == CheckSource.Fns).Status.ShouldBe(CheckStatus.Succeeded);
        application.Status.ShouldBe(ApplicationStatus.Approved);
    }

    [Fact]
    public async Task Unavailable_critical_source_routes_to_manual_review_and_notifies_managers()
    {
        _sources.FailuresLeft[CheckSource.CreditBureau] = 10;
        var application = Submit(amount: 1_500_000, termMonths: 12);

        await RunPipelineAsync(application);

        application.Status.ShouldBe(ApplicationStatus.ManualReview);
        application.Checks.First(c => c.Source == CheckSource.CreditBureau).Status.ShouldBe(CheckStatus.Failed);
        application.LatestScoring.ShouldBeNull();
        _feed.Items.Where(f => f.UserId == _manager.Id).ShouldHaveSingleItem().Type.ShouldBe(FeedItemType.ManualReview);
    }

    [Fact]
    public async Task Large_amount_goes_to_manual_review_with_recommendation()
    {
        var application = Submit(amount: 8_000_000, termMonths: 24, revenue: 180_000_000, netProfit: 16_000_000, monthlyDebtPayments: 1_200_000);

        await RunPipelineAsync(application);

        application.Status.ShouldBe(ApplicationStatus.ManualReview);
        application.LatestDecision!.Type.ShouldBe(DecisionType.ManualReview);
        application.LatestDecision.RecommendedType.ShouldBe(DecisionType.Approved);
        application.LatestDecision.IsFinal.ShouldBeFalse();
    }

    [Fact]
    public async Task Cancelled_application_is_skipped_by_pending_jobs()
    {
        var application = Submit(amount: 1_500_000, termMonths: 12);
        application.Cancel(_clientId, null, TestData.Now);

        await RunPipelineAsync(application);

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        application.Checks.ShouldBeEmpty();
        _jobs.Items.ShouldHaveSingleItem().Status.ShouldBe(ProcessingJobStatus.Done);
    }

    private CreditApplication Submit(decimal amount, int termMonths, decimal revenue = 48_000_000, decimal? netProfit = 7_200_000, decimal monthlyDebtPayments = 150_000)
    {
        var company = TestData.Company(memberUserId: _clientId, annualRevenue: revenue, netProfit: netProfit, monthlyDebtPayments: monthlyDebtPayments);
        var application = CreditApplication.Create(company, TestData.Product(), _clientId, amount, termMonths, LoanPurpose.WorkingCapital, null, TestData.Now);
        _applications.Add(application);
        _jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.Validate, TestData.Now, TimeSpan.Zero));
        return application;
    }

    private async Task RunPipelineAsync(CreditApplication application)
    {
        var pipeline = CreatePipeline();
        for (var guard = 0; guard < 10; guard++)
        {
            var job = await _jobs.ClaimNextAsync(_clock.GetUtcNow(), CancellationToken.None);
            if (job is null)
            {
                return;
            }

            await pipeline.ExecuteAsync(job, CancellationToken.None);
        }

        throw new InvalidOperationException("Pipeline did not finish within 10 jobs.");
    }

    private ApplicationPipeline CreatePipeline()
    {
        var policyOptions = new DecisionPolicyOptions();
        return new ApplicationPipeline(
            _applications,
            _jobs,
            _sources,
            _sources,
            _sources,
            _sources,
            new RuleBasedScoringService(),
            new DecisionPolicy(policyOptions),
            new FeedService(_feed, new InMemoryUsersByRole(_manager), _clock),
            _notifier,
            _unitOfWork,
            _clock,
            Options.Create(new ProcessingOptions { StageDelaySeconds = 0 }),
            Options.Create(policyOptions),
            NullLogger<ApplicationPipeline>.Instance);
    }
}
