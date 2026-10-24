using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Financing;
using AlfaCredit.UnitTests.Applications;
using AlfaCredit.UnitTests.Fakes;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Financing;

public sealed class FinancingPreviewServiceTests
{
    private static readonly Actor Client = new(Guid.NewGuid(), UserRole.Client);
    private static readonly Actor OtherClient = new(Guid.NewGuid(), UserRole.Client);
    private static readonly Actor Manager = new(Guid.NewGuid(), UserRole.Manager);
    private static readonly DateOnly Today = Core.Common.BankCalendar.Today(TestData.Now);

    private readonly Core.Domain.Companies.Company _company = TestData.Company(memberUserId: Client.UserId);
    private readonly FinancingPreviewOptions _options = new();

    [Fact]
    public async Task Company_member_gets_options_without_anything_being_saved()
    {
        var service = CreateService();

        var preview = await service.PreviewAsync(Query(), Client, CancellationToken.None);

        preview.Company.ShouldBe(_company);
        preview.Options.Select(o => o.Product.Code).ShouldBe(["working-capital"]);
        preview.CalculatedAt.ShouldBe(TestData.Now);
    }

    [Fact]
    public async Task Bank_employee_can_preview_for_any_company_but_other_clients_cannot()
    {
        var service = CreateService();

        var preview = await service.PreviewAsync(Query(), Manager, CancellationToken.None);
        preview.Options.ShouldNotBeEmpty();

        await Should.ThrowAsync<ForbiddenException>(() => service.PreviewAsync(Query(), OtherClient, CancellationToken.None));
    }

    [Fact]
    public async Task Unknown_company_is_a_field_error()
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ValidationException>(() => service.PreviewAsync(Query() with { CompanyId = Guid.NewGuid() }, Client, CancellationToken.None));

        exception.Errors.Keys.ShouldBe(["companyId"]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(731)]
    public async Task Date_in_the_past_or_too_far_ahead_is_rejected(int daysFromToday)
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ValidationException>(() =>
            service.PreviewAsync(Query() with { NeedByDate = Today.AddDays(daysFromToday) }, Client, CancellationToken.None));

        exception.Errors.Keys.ShouldBe(["needByDate"]);
    }

    [Fact]
    public async Task Today_and_two_years_ahead_are_accepted()
    {
        var service = CreateService();

        (await service.PreviewAsync(Query() with { NeedByDate = Today }, Client, CancellationToken.None)).DaysUntilNeeded.ShouldBe(0);
        (await service.PreviewAsync(Query() with { NeedByDate = Today.AddDays(730) }, Client, CancellationToken.None)).DaysUntilNeeded.ShouldBe(730);
    }

    [Fact]
    public async Task Date_window_is_checked_against_the_moscow_date()
    {
        // 20.10.2026 21:30 UTC: по Москве уже 21.10.2026
        var lateEvening = new DateTimeOffset(2026, 10, 20, 21, 30, 0, TimeSpan.Zero);
        var service = CreateService(lateEvening);

        var exception = await Should.ThrowAsync<ValidationException>(() =>
            service.PreviewAsync(Query() with { NeedByDate = new DateOnly(2026, 10, 20) }, Client, CancellationToken.None));
        exception.Errors.Keys.ShouldBe(["needByDate"]);

        var preview = await service.PreviewAsync(Query() with { NeedByDate = new DateOnly(2026, 10, 21) }, Client, CancellationToken.None);
        preview.CalculationDate.ShouldBe(new DateOnly(2026, 10, 21));
        preview.DaysUntilNeeded.ShouldBe(0);
    }

    [Fact]
    public async Task Non_positive_amount_and_term_are_reported_together()
    {
        var service = CreateService();

        var exception = await Should.ThrowAsync<ValidationException>(() =>
            service.PreviewAsync(Query() with { Amount = 0, TermMonths = 0 }, Client, CancellationToken.None));

        exception.Errors.Keys.ShouldBe(["amount", "termMonths"], ignoreOrder: true);
    }

    private FinancingPreviewQuery Query() => new(_company.Id, 1_500_000, LoanPurpose.WorkingCapital, Today.AddDays(30), 12);

    private FinancingPreviewService CreateService(DateTimeOffset? now = null) => new(
        new InMemoryCompanyRepository(_company),
        new InMemoryProductRepository(TestData.Product()),
        new FinancingAdvisor(_options, new DecisionPolicyOptions()),
        new FixedTimeProvider(now ?? TestData.Now),
        Options.Create(_options));
}
