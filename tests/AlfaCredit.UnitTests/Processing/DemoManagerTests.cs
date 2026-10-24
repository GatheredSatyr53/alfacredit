using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
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

/// <summary>Демо-режим: смоделированный ответ менеджера по заявкам в ручном рассмотрении.</summary>
public sealed class DemoManagerTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

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
    public async Task Approval_creates_offer_with_conditions_marked_as_simulated()
    {
        var application = SubmitLargeApplication();

        await RunAsync(approves: true);

        application.Status.ShouldBe(ApplicationStatus.ApprovedWithConditions);
        var decision = application.FinalDecision.ShouldNotBeNull();
        decision.DecidedBy.ShouldBe(DecisionActor.Manager);
        decision.IsSimulated.ShouldBeTrue();
        decision.IsFinal.ShouldBeTrue();
        decision.ApprovedAmount.ShouldBe(8_000_000, "половина выручки 180 млн не ограничивает сумму");
        decision.InterestRate.ShouldBe(21m, "базовая 19.5 + надбавка грейда B");
        decision.Reasons.Select(r => r.Code).ShouldContain(DemoManagerPolicy.SimulatedReasonCode);
        decision.Reasons.Select(r => r.Code).ShouldContain(DemoManagerPolicy.ApprovedReasonCode);
        decision.Conditions!.ShouldContain("ставка 21 %");
        application.CurrentOffer.ShouldNotBeNull().Amount.ShouldBe(8_000_000);
        application.AvailableActions(ActorType.Client).ShouldBe(["acceptOffer", "declineOffer"], ignoreOrder: true);
        application.History[^1].Comment!.ShouldContain("Смоделированный ответ менеджера (демо-режим)");
        application.History[^1].ActorType.ShouldBe(ActorType.Manager);
        application.History.Select(h => h.ToStatus).ShouldContain(ApplicationStatus.ManualReview);
        _jobs.Items.Select(j => j.Stage).ShouldContain(ProcessingStage.ManagerDecision);
        _jobs.Items.ShouldAllBe(j => j.Status == ProcessingJobStatus.Done);
        _feed.Items.Where(f => f.UserId == _clientId).Select(f => f.Type).ShouldContain(FeedItemType.OfferReady);
        _feed.Items.Where(f => f.UserId == _clientId && f.Type == FeedItemType.OfferReady).ShouldHaveSingleItem().Body.ShouldContain("смоделирован");
        _notifier.Notifications.Select(n => n.Status).ShouldContain(ApplicationStatus.ApprovedWithConditions);

        // Клиент продолжает оформление как после обычного одобрения
        application.AcceptOffer(_clientId, _clock.GetUtcNow());
        application.Status.ShouldBe(ApplicationStatus.ContractSigning);
    }

    [Fact]
    public async Task Rejection_is_final_and_keeps_the_handoff_reasons()
    {
        var application = SubmitLargeApplication();

        await RunAsync(approves: false);

        application.Status.ShouldBe(ApplicationStatus.Rejected);
        application.IsTerminal.ShouldBeTrue();
        var decision = application.FinalDecision.ShouldNotBeNull();
        decision.IsSimulated.ShouldBeTrue();
        decision.Type.ShouldBe(DecisionType.Rejected);
        var codes = decision.Reasons.Select(r => r.Code).ToArray();
        codes[0].ShouldBe(DemoManagerPolicy.RejectedReasonCode);
        codes.ShouldContain("amount_above_auto_limit", "причина передачи менеджеру сохраняется в объяснении");
        codes.ShouldContain(DemoManagerPolicy.SimulatedReasonCode);
        decision.Reasons[0].ClientExplanation.ShouldContain("новую заявку");
        application.CurrentOffer.ShouldBeNull();
        application.Decisions.Count.ShouldBe(2, "рекомендация автоматики и смоделированное решение менеджера");
        _feed.Items.Where(f => f.UserId == _clientId).Select(f => f.Type).ShouldContain(FeedItemType.Rejected);
    }

    [Fact]
    public async Task Cancelled_application_gets_no_simulated_decision()
    {
        var application = SubmitLargeApplication();
        var pipeline = CreatePipeline(true);
        for (var i = 0; i < 4; i++)
        {
            await pipeline.ExecuteAsync((await ClaimAsync()).ShouldNotBeNull(), CancellationToken.None);
        }

        application.Status.ShouldBe(ApplicationStatus.ManualReview);
        application.Cancel(_clientId, null, _clock.GetUtcNow());

        var managerJob = (await ClaimAsync()).ShouldNotBeNull();
        managerJob.Stage.ShouldBe(ProcessingStage.ManagerDecision);
        await pipeline.ExecuteAsync(managerJob, CancellationToken.None);

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        application.FinalDecision.ShouldBeNull();
        managerJob.Status.ShouldBe(ProcessingJobStatus.Done);
    }

    [Fact]
    public async Task Disabled_demo_mode_leaves_application_with_the_manager()
    {
        var application = SubmitLargeApplication();

        await RunAsync(approves: true, enabled: false);

        application.Status.ShouldBe(ApplicationStatus.ManualReview);
        _jobs.Items.Select(j => j.Stage).ShouldNotContain(ProcessingStage.ManagerDecision);
    }

    [Fact]
    public async Task Manual_review_after_source_outage_gets_a_conservative_simulated_approval()
    {
        _sources.FailuresLeft[CheckSource.CreditBureau] = 10;
        var application = Submit(amount: 1_500_000, termMonths: 12);

        await RunAsync(approves: true);

        application.Status.ShouldBe(ApplicationStatus.ApprovedWithConditions);
        var decision = application.FinalDecision.ShouldNotBeNull();
        decision.InterestRate.ShouldBe(22.5m, "без скоринга менеджер берёт надбавку грейда C");
        decision.Conditions!.ShouldContain("выписку по расчётному счёту");
        decision.IsSimulated.ShouldBeTrue();
    }

    [Fact]
    public async Task Permanent_stage_failure_in_demo_mode_schedules_the_simulated_manager()
    {
        var application = Submit(amount: 1_500_000, termMonths: 12);
        var job = (await ClaimAsync()).ShouldNotBeNull();
        job.Stage.ShouldBe(ProcessingStage.Validate);
        var handler = new ProcessingFailureHandler(
            _jobs,
            _applications,
            Feed(),
            _unitOfWork,
            _clock,
            Options.Create(new ProcessingOptions { MaxAttempts = 1 }),
            Options.Create(new DemoManagerOptions { Enabled = true, DelaySeconds = 0 }),
            NullLogger<ProcessingFailureHandler>.Instance);

        // задание создано с MaxAttempts = 3, поэтому исчерпываем попытки
        job.MarkFailed("boom", _clock.GetUtcNow(), TimeSpan.Zero);
        job.MarkRunning(_clock.GetUtcNow(), Lease);
        job.MarkFailed("boom", _clock.GetUtcNow(), TimeSpan.Zero);
        job.MarkRunning(_clock.GetUtcNow(), Lease);
        await handler.HandleAsync(job.Id, new InvalidOperationException("boom"), CancellationToken.None);

        application.Status.ShouldBe(ApplicationStatus.ManualReview);
        _jobs.Items.Where(j => j.Stage == ProcessingStage.ManagerDecision && j.Status == ProcessingJobStatus.Pending).ShouldHaveSingleItem();
    }

    [Fact]
    public void Policy_caps_amount_to_half_of_revenue_and_marks_the_decision()
    {
        var company = TestData.Company(memberUserId: _clientId, annualRevenue: 10_000_000, netProfit: 1_000_000);
        var application = CreditApplication.Create(company, TestData.Product(), _clientId, 8_000_000, 24, LoanPurpose.WorkingCapital, null, TestData.Now);

        var approve = DemoManagerPolicy.Approve(application, TestData.Now);
        approve.Type.ShouldBe(DecisionType.ApprovedWithConditions);
        approve.ApprovedAmount.ShouldBe(5_000_000);
        approve.InterestRate.ShouldBe(22.5m, "без скоринга берётся грейд C");
        approve.MonthlyPayment.ShouldBe(LoanMath.MonthlyPayment(5_000_000, 22.5m, 24));
        approve.Reasons.Select(r => r.Code).ShouldBe([DemoManagerPolicy.ApprovedReasonCode, "amount_reduced", DemoManagerPolicy.SimulatedReasonCode]);
        approve.IsSimulated.ShouldBeTrue();

        var reject = DemoManagerPolicy.Reject(application, TestData.Now);
        reject.Type.ShouldBe(DecisionType.Rejected);
        reject.IsFinal.ShouldBeTrue();
        reject.IsSimulated.ShouldBeTrue();
        reject.Reasons.Select(r => r.Code).ShouldBe([DemoManagerPolicy.RejectedReasonCode, DemoManagerPolicy.SimulatedReasonCode]);
    }

    private CreditApplication SubmitLargeApplication() =>
        Submit(amount: 8_000_000, termMonths: 24, revenue: 180_000_000, netProfit: 16_000_000, monthlyDebtPayments: 1_200_000, annualExpenses: 160_000_000);

    private CreditApplication Submit(decimal amount, int termMonths, decimal revenue = 48_000_000, decimal? netProfit = 7_200_000, decimal monthlyDebtPayments = 150_000, decimal? annualExpenses = null)
    {
        var company = TestData.Company(memberUserId: _clientId, annualRevenue: revenue, netProfit: netProfit, monthlyDebtPayments: monthlyDebtPayments, annualExpenses: annualExpenses);
        var application = CreditApplication.Create(company, TestData.Product(), _clientId, amount, termMonths, LoanPurpose.WorkingCapital, null, TestData.Now);
        _applications.Add(application);
        _jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.Validate, TestData.Now, TimeSpan.Zero));
        return application;
    }

    private async Task RunAsync(bool approves, bool enabled = true)
    {
        var pipeline = CreatePipeline(approves, enabled);
        for (var guard = 0; guard < 10; guard++)
        {
            var job = await ClaimAsync();
            if (job is null)
            {
                return;
            }

            await pipeline.ExecuteAsync(job, CancellationToken.None);
        }

        throw new InvalidOperationException("Pipeline did not finish within 10 jobs.");
    }

    private Task<ProcessingJob?> ClaimAsync() => _jobs.ClaimNextAsync(_clock.GetUtcNow(), Lease, CancellationToken.None);

    private FeedService Feed() => new(_feed, new InMemoryUsersByRole(_manager), _clock);

    private ApplicationPipeline CreatePipeline(bool approves, bool enabled = true)
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
            new FakeDisbursementGateway(),
            new DecisionPolicy(policyOptions),
            Feed(),
            _notifier,
            _unitOfWork,
            _clock,
            Options.Create(new ProcessingOptions { StageDelaySeconds = 0 }),
            Options.Create(policyOptions),
            Options.Create(new DemoManagerOptions { Enabled = enabled, DelaySeconds = 0, ApprovalProbability = 0.5 }),
            new FixedDemoManagerChooser(approves),
            NullLogger<ApplicationPipeline>.Instance);
    }
}
