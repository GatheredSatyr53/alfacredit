using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
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

/// <summary>Этап Disburse: выдача по подписанному договору через банковское ядро.</summary>
public sealed class DisbursementPipelineTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    private readonly User _manager = User.Create("manager", "hash", "Алексей Петров", UserRole.Manager, TestData.Now);
    private readonly InMemoryApplicationRepository _applications = new();
    private readonly InMemoryJobRepository _jobs = new();
    private readonly InMemoryFeedRepository _feed = new();
    private readonly FakeDisbursementGateway _bank = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(TestData.Now.AddHours(3));

    [Fact]
    public async Task Signed_application_is_disbursed_and_completed()
    {
        var (application, contract) = SignedApplication();

        var job = await ClaimAsync();
        await CreatePipeline().ExecuteAsync(job, CancellationToken.None);

        application.Status.ShouldBe(ApplicationStatus.Disbursed);
        var disbursement = application.CurrentDisbursement.ShouldNotBeNull();
        disbursement.Status.ShouldBe(DisbursementStatus.Completed);
        disbursement.PaymentOrderNumber.ShouldBe("PO-TEST-0001");
        disbursement.Account.ShouldBe(contract.DisbursementAccount);
        disbursement.Amount.ShouldBe(1_500_000);
        _bank.Transfers.ShouldHaveSingleItem().ShouldBe(contract);
        job.Status.ShouldBe(ProcessingJobStatus.Done);
        _jobs.Items.Count.ShouldBe(1, "после выдачи этапов больше нет");
        _feed.Items.Where(f => f.UserId == LoanContractTests.UserId).Select(f => f.Type).ShouldContain(FeedItemType.Disbursed);
        _feed.Items.Where(f => f.UserId == _manager.Id).Select(f => f.Type).ShouldContain(FeedItemType.Disbursed);
        _notifier.Notifications.Select(n => n.Status).ShouldContain(ApplicationStatus.Disbursed);
    }

    [Fact]
    public async Task Failed_transfer_is_recorded_and_the_retry_succeeds()
    {
        var (application, _) = SignedApplication();
        _bank.FailuresLeft = 1;
        var pipeline = CreatePipeline();

        var job = await ClaimAsync();
        await Should.ThrowAsync<DisbursementFailedException>(() => pipeline.ExecuteAsync(job, CancellationToken.None));

        application.Status.ShouldBe(ApplicationStatus.Signed);
        var failed = application.CurrentDisbursement.ShouldNotBeNull();
        failed.Status.ShouldBe(DisbursementStatus.Failed);
        failed.Error!.ShouldContain("outage");
        _unitOfWork.SaveCalls.ShouldBe(1, "неудачная попытка сохранена для аудита");

        // Обработчик сбоя вернул задание в очередь, воркер взял его снова
        job.MarkFailed("simulated", _clock.GetUtcNow(), TimeSpan.Zero).ShouldBeTrue();
        var retry = await ClaimAsync();
        retry.ShouldBe(job);
        await pipeline.ExecuteAsync(retry, CancellationToken.None);

        application.Status.ShouldBe(ApplicationStatus.Disbursed);
        application.Disbursements.Count.ShouldBe(2);
        application.CurrentDisbursement!.Status.ShouldBe(DisbursementStatus.Completed);
        _bank.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task Permanent_disbursement_failure_keeps_status_and_alerts_managers()
    {
        var (application, _) = SignedApplication(maxAttempts: 1);
        var job = await ClaimAsync();
        var handler = new ProcessingFailureHandler(
            _jobs,
            _applications,
            new FeedService(_feed, new InMemoryUsersByRole(_manager), _clock),
            _unitOfWork,
            _clock,
            Options.Create(new ProcessingOptions()),
            NullLogger<ProcessingFailureHandler>.Instance);

        await handler.HandleAsync(job.Id, new DisbursementFailedException("core banking down"), CancellationToken.None);

        job.Status.ShouldBe(ProcessingJobStatus.Failed);
        application.Status.ShouldBe(ApplicationStatus.Signed, "из Signed нет перехода в ручное рассмотрение");
        var alert = _feed.Items.Where(f => f.UserId == _manager.Id).ShouldHaveSingleItem();
        alert.Type.ShouldBe(FeedItemType.ManualReview);
        alert.Title.ShouldContain("Disburse");
        _feed.Items.Where(f => f.UserId == LoanContractTests.UserId).ShouldHaveSingleItem().Type.ShouldBe(FeedItemType.StatusChanged);
    }

    private (CreditApplication Application, LoanContract Contract) SignedApplication(int maxAttempts = 3)
    {
        var (application, contract) = LoanContractTests.SigningApplication();
        contract.RequestCode("483921", TestData.Now.AddHours(2), TimeSpan.FromMinutes(10));
        application.SignContract("483921", LoanContractTests.UserId, "Иванова Мария Сергеевна", TestData.Now.AddHours(2));
        _applications.Add(application);
        _jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.Disburse, _clock.GetUtcNow(), TimeSpan.Zero, maxAttempts));
        return (application, contract);
    }

    private async Task<ProcessingJob> ClaimAsync() =>
        (await _jobs.ClaimNextAsync(_clock.GetUtcNow(), Lease, CancellationToken.None)).ShouldNotBeNull();

    private ApplicationPipeline CreatePipeline()
    {
        var policyOptions = new DecisionPolicyOptions();
        var sources = new FakeSources();
        return new ApplicationPipeline(
            _applications,
            _jobs,
            sources,
            sources,
            sources,
            sources,
            new RuleBasedScoringService(),
            _bank,
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
