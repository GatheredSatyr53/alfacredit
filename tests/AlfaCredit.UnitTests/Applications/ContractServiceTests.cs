using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Applications;
using AlfaCredit.Core.Features.Contracts;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Processing;
using AlfaCredit.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

/// <summary>Сценарий завершения заявки: принятие предложения, запрос кода, подписание, постановка выдачи в очередь.</summary>
public sealed class ContractServiceTests
{
    private readonly User _client = User.Create("client_good", "hash", "Иванова Мария Сергеевна", UserRole.Client, TestData.Now);
    private readonly User _manager = User.Create("manager", "hash", "Алексей Петров", UserRole.Manager, TestData.Now);
    private readonly Company _company;
    private readonly CreditProduct _product = TestData.Product();
    private readonly InMemoryApplicationRepository _applications = new();
    private readonly InMemoryJobRepository _jobs = new();
    private readonly InMemoryFeedRepository _feed = new();
    private readonly RecordingNotifier _notifier = new();
    private readonly CountingUnitOfWork _unitOfWork = new();
    private readonly FixedTimeProvider _clock = new(TestData.Now.AddHours(1));
    private readonly FixedCodeGenerator _codes = new("483921");

    public ContractServiceTests()
    {
        _company = TestData.Company(memberUserId: _client.Id);
    }

    private Actor Client => new(_client.Id, UserRole.Client);

    private Actor Manager => new(_manager.Id, UserRole.Manager);

    [Fact]
    public async Task Accepting_offer_creates_contract_and_signing_schedules_disbursement()
    {
        var application = ApprovedApplication();

        var accepted = await Applications().AcceptOfferAsync(application.Id, Client, CancellationToken.None);

        accepted.Status.ShouldBe(ApplicationStatus.ContractSigning);
        var contract = accepted.CurrentContract.ShouldNotBeNull();
        contract.Status.ShouldBe(ContractStatus.AwaitingSignature);
        contract.LenderName.ShouldBe("AlfaCredit (демонстрационный банк)");
        contract.BorrowerName.ShouldBe(_company.Name);
        contract.BorrowerInn.ShouldBe(_company.Inn);
        contract.SigningDeadline.ShouldBe(_clock.GetUtcNow().AddDays(5));
        _feed.Items.Where(f => f.UserId == _client.Id).Select(f => f.Type).ShouldContain(FeedItemType.ContractReady);
        _jobs.Items.ShouldBeEmpty("выдача ставится в очередь только после подписания");

        var (_, viewed) = await Contracts().GetAsync(application.Id, Manager, CancellationToken.None);
        viewed.ShouldBe(contract);

        var issued = await Contracts().RequestSigningCodeAsync(application.Id, Client, CancellationToken.None);
        issued.RevealedCode.ShouldBe("483921");
        issued.AttemptsLeft.ShouldBe(5);
        issued.CodeExpiresAt.ShouldBe(_clock.GetUtcNow().AddMinutes(10));
        _feed.Items.Where(f => f.Type == FeedItemType.SigningCode).ShouldHaveSingleItem().Body.ShouldContain("483921");

        var wrong = await Should.ThrowAsync<ValidationException>(() => Contracts().SignAsync(application.Id, "000000", Client, CancellationToken.None));
        wrong.Errors["code"][0].ShouldContain("Осталось попыток: 4");
        var savesBeforeSigning = _unitOfWork.SaveCalls;
        savesBeforeSigning.ShouldBeGreaterThan(0, "израсходованная попытка сохраняется");
        contract.AttemptsLeft.ShouldBe(4);

        var signed = await Contracts().SignAsync(application.Id, "483921", Client, CancellationToken.None);

        signed.Status.ShouldBe(ApplicationStatus.Signed);
        contract.Status.ShouldBe(ContractStatus.Signed);
        contract.SignedByName.ShouldBe("Иванова Мария Сергеевна");
        _jobs.Items.ShouldHaveSingleItem().Stage.ShouldBe(ProcessingStage.Disburse);
        _feed.Items.Where(f => f.UserId == _client.Id).Select(f => f.Type).ShouldContain(FeedItemType.ContractSigned);
        _feed.Items.Where(f => f.UserId == _manager.Id).Select(f => f.Type).ShouldContain(FeedItemType.ContractSigned);
        _notifier.Notifications.Select(n => n.Status).ShouldContain(ApplicationStatus.Signed);
    }

    [Fact]
    public async Task Code_cannot_be_requested_without_contract_or_by_bank_staff()
    {
        var application = ApprovedApplication();

        await Should.ThrowAsync<NotFoundException>(() => Contracts().GetAsync(application.Id, Client, CancellationToken.None));
        await Should.ThrowAsync<ConflictException>(() => Contracts().RequestSigningCodeAsync(application.Id, Client, CancellationToken.None));

        await Applications().AcceptOfferAsync(application.Id, Client, CancellationToken.None);

        await Should.ThrowAsync<ForbiddenException>(() => Contracts().RequestSigningCodeAsync(application.Id, Manager, CancellationToken.None));
        await Should.ThrowAsync<ForbiddenException>(() => Contracts().SignAsync(application.Id, "483921", Manager, CancellationToken.None));
        var stranger = new Actor(Guid.NewGuid(), UserRole.Client);
        await Should.ThrowAsync<ForbiddenException>(() => Contracts().GetAsync(application.Id, stranger, CancellationToken.None));
    }

    [Fact]
    public async Task Unsigned_contract_expires_and_cancels_the_application()
    {
        var application = ApprovedApplication();
        await Applications().AcceptOfferAsync(application.Id, Client, CancellationToken.None);
        await Contracts().RequestSigningCodeAsync(application.Id, Client, CancellationToken.None);

        _clock.Advance(TimeSpan.FromDays(6));

        await Should.ThrowAsync<ConflictException>(() => Contracts().SignAsync(application.Id, "483921", Client, CancellationToken.None));
        await Should.ThrowAsync<ConflictException>(() => Contracts().RequestSigningCodeAsync(application.Id, Client, CancellationToken.None));

        var expiry = new OfferExpiryService(_applications, Feed(), _unitOfWork, _clock, NullLogger<OfferExpiryService>.Instance);
        (await expiry.ExpireAsync(CancellationToken.None)).ShouldBe(1);

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        application.CurrentContract!.Status.ShouldBe(ContractStatus.Expired);
        _feed.Items.Where(f => f.UserId == _client.Id).Select(f => f.Title).ShouldContain(t => t.Contains("истёк"));
    }

    [Fact]
    public async Task Client_can_withdraw_from_signing()
    {
        var application = ApprovedApplication();
        await Applications().AcceptOfferAsync(application.Id, Client, CancellationToken.None);

        var cancelled = await Applications().CancelAsync(application.Id, Client, null, CancellationToken.None);

        cancelled.Status.ShouldBe(ApplicationStatus.Cancelled);
        cancelled.CurrentContract!.Status.ShouldBe(ContractStatus.Cancelled);
        _feed.Items.Where(f => f.UserId == _client.Id).Select(f => f.Title).ShouldContain(t => t.Contains("Подписание договора"));
        await Should.ThrowAsync<ConflictException>(() => Contracts().RequestSigningCodeAsync(application.Id, Client, CancellationToken.None));
    }

    [Fact]
    public void Document_renders_terms_schedule_and_signature()
    {
        var (application, contract) = LoanContractTests.SigningApplication();
        var schedule = ContractSchedule.Build(application, contract);

        var draft = ContractDocumentRenderer.Render(application, contract, schedule);
        draft.ShouldContain("<!DOCTYPE html>");
        draft.ShouldContain("Кредитный договор");
        draft.ShouldContain("Ромашка");
        draft.ShouldContain("1 500 000,00 ₽");
        draft.ShouldContain("19.5 % годовых");
        draft.ShouldContain("ожидает подписания");
        schedule.Count.ShouldBe(12);
        schedule[0].DueDate.ShouldBe(DateOnly.FromDateTime(contract.CreatedAt.UtcDateTime).AddMonths(1));

        contract.RequestCode("483921", TestData.Now.AddHours(2), TimeSpan.FromMinutes(10));
        application.SignContract("483921", LoanContractTests.UserId, "Иванова Мария Сергеевна", TestData.Now.AddHours(2));
        var signed = ContractDocumentRenderer.Render(application, contract, ContractSchedule.Build(application, contract));
        signed.ShouldContain("Иванова Мария Сергеевна");
        signed.ShouldContain("Подписано");
        signed.ShouldNotContain("483921", customMessage: "код подтверждения не попадает в документ");
    }

    private CreditApplication ApprovedApplication()
    {
        var application = CreditApplication.Create(_company, _product, _client.Id, 1_500_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);
        application.BeginValidation(TestData.Now);
        application.BeginEnrichment(TestData.Now);
        application.BeginScoring(TestData.Now);
        application.BeginDeciding(TestData.Now);
        var decision = Decision.Create(application.Id, DecisionType.Approved, DecisionActor.Auto, null, null, 1_500_000, 12, 19.5m, LoanMath.MonthlyPayment(1_500_000, 19.5m, 12), null, [], TestData.Now);
        application.ApplyDecision(decision, TimeSpan.FromDays(7), TestData.Now);
        _applications.Add(application);
        return application;
    }

    private FeedService Feed() => new(_feed, new InMemoryUsersByRole(_client, _manager), _clock);

    private ApplicationService Applications() => new(
        _applications,
        new InMemoryCompanyRepository(_company),
        new InMemoryProductRepository(_product),
        _jobs,
        Feed(),
        _notifier,
        _unitOfWork,
        _clock,
        Options.Create(new ProcessingOptions { StageDelaySeconds = 0 }),
        Options.Create(new ContractOptions()),
        NullLogger<ApplicationService>.Instance);

    private ContractService Contracts() => new(
        Applications(),
        new InMemoryUsersByRole(_client, _manager),
        _jobs,
        Feed(),
        _notifier,
        _codes,
        _unitOfWork,
        _clock,
        Options.Create(new ContractOptions()),
        Options.Create(new ProcessingOptions { StageDelaySeconds = 0 }),
        NullLogger<ContractService>.Instance);

    private sealed class FixedCodeGenerator(string code) : ISigningCodeGenerator
    {
        public string Next() => code;
    }
}
