using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Applications;
using AlfaCredit.UnitTests.Fakes;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class ApplicationServiceTests
{
    private static readonly Actor Client = new(Guid.NewGuid(), UserRole.Client);
    private static readonly Actor OtherClient = new(Guid.NewGuid(), UserRole.Client);
    private static readonly Actor Manager = new(Guid.NewGuid(), UserRole.Manager);

    private readonly Company _company = TestData.Company(memberUserId: Client.UserId);
    private readonly CreditProduct _product = TestData.Product();
    private readonly InMemoryApplicationRepository _applications = new();
    private readonly CountingUnitOfWork _unitOfWork = new();

    [Fact]
    public async Task CreateAsync_saves_submitted_application_for_company_member()
    {
        var service = CreateService();

        var application = await service.CreateAsync(Command(), Client, CancellationToken.None);

        application.Status.ShouldBe(ApplicationStatus.Submitted);
        application.CreatedByUserId.ShouldBe(Client.UserId);
        _applications.Items.ShouldContain(application);
        _unitOfWork.SaveCalls.ShouldBe(1);
    }

    [Fact]
    public async Task CreateAsync_rejects_user_who_is_not_a_member_of_the_company()
    {
        var service = CreateService();

        await Should.ThrowAsync<ForbiddenException>(() => service.CreateAsync(Command(), OtherClient, CancellationToken.None));

        _applications.Items.ShouldBeEmpty();
        _unitOfWork.SaveCalls.ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_rejects_bank_employees()
    {
        var service = CreateService();

        await Should.ThrowAsync<ForbiddenException>(() => service.CreateAsync(Command(), Manager, CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_reports_unknown_company_and_product_by_field()
    {
        var service = CreateService();
        var command = Command() with { CompanyId = Guid.NewGuid(), ProductCode = "no-such-product" };

        var exception = await Should.ThrowAsync<ValidationException>(() => service.CreateAsync(command, Client, CancellationToken.None));

        exception.Errors.Keys.ShouldBe(["companyId", "productCode"], ignoreOrder: true);
        exception.Errors["productCode"][0].ShouldContain("no-such-product");
    }

    [Fact]
    public async Task CreateAsync_finds_product_code_case_insensitively()
    {
        var service = CreateService();

        var application = await service.CreateAsync(Command() with { ProductCode = " WORKING-CAPITAL " }, Client, CancellationToken.None);

        application.ProductId.ShouldBe(_product.Id);
    }

    [Fact]
    public async Task GetAsync_allows_company_member_and_bank_but_not_other_clients()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(), Client, CancellationToken.None);

        (await service.GetAsync(created.Id, Client, CancellationToken.None)).ShouldBeSameAs(created);
        (await service.GetAsync(created.Id, Manager, CancellationToken.None)).ShouldBeSameAs(created);
        await Should.ThrowAsync<ForbiddenException>(() => service.GetAsync(created.Id, OtherClient, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_throws_not_found_for_unknown_id()
    {
        var service = CreateService();

        await Should.ThrowAsync<NotFoundException>(() => service.GetAsync(Guid.NewGuid(), Client, CancellationToken.None));
    }

    [Fact]
    public async Task ListAsync_scopes_clients_to_their_companies_and_bank_to_everything()
    {
        var service = CreateService();
        await service.CreateAsync(Command(), Client, CancellationToken.None);

        var clientPage = await service.ListAsync(new ApplicationListQuery(null), Client, CancellationToken.None);
        var otherPage = await service.ListAsync(new ApplicationListQuery(null), OtherClient, CancellationToken.None);
        var bankPage = await service.ListAsync(new ApplicationListQuery(ApplicationStatus.Submitted), Manager, CancellationToken.None);

        clientPage.TotalCount.ShouldBe(1);
        otherPage.TotalCount.ShouldBe(0);
        bankPage.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task CancelAsync_cancels_own_application_and_rejects_bank_employee()
    {
        var service = CreateService();
        var created = await service.CreateAsync(Command(), Client, CancellationToken.None);

        var cancelled = await service.CancelAsync(created.Id, Client, "Передумали", CancellationToken.None);

        cancelled.Status.ShouldBe(ApplicationStatus.Cancelled);
        _unitOfWork.SaveCalls.ShouldBe(2);
        await Should.ThrowAsync<ForbiddenException>(() => service.CancelAsync(created.Id, Manager, null, CancellationToken.None));
    }

    private ApplicationService CreateService() => new(
        _applications,
        new InMemoryCompanyRepository(_company),
        new InMemoryProductRepository(_product),
        _unitOfWork,
        new FixedTimeProvider(TestData.Now));

    private CreateApplicationCommand Command() =>
        new(_company.Id, _product.Code, 1_500_000, 12, LoanPurpose.WorkingCapital, "Закупка товара");
}
