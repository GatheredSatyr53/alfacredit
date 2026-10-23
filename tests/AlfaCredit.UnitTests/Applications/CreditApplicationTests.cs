using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Products;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class CreditApplicationTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Create_submits_application_and_records_history()
    {
        var company = TestData.Company(memberUserId: UserId);
        var product = TestData.Product();

        var application = CreditApplication.Create(company, product, UserId, 1_500_000, 12, LoanPurpose.WorkingCapital, "  Закупка товара  ", TestData.Now);

        application.Status.ShouldBe(ApplicationStatus.Submitted);
        application.CompanyId.ShouldBe(company.Id);
        application.ProductId.ShouldBe(product.Id);
        application.PurposeDescription.ShouldBe("Закупка товара");
        application.CreatedAt.ShouldBe(TestData.Now);
        application.History.Count.ShouldBe(1);
        application.History[0].FromStatus.ShouldBeNull();
        application.History[0].ToStatus.ShouldBe(ApplicationStatus.Submitted);
        application.History[0].ActorType.ShouldBe(ActorType.Client);
        application.History[0].ActorUserId.ShouldBe(UserId);
        application.AvailableActions(ActorType.Client).ShouldBe(["cancel"]);
        application.AvailableActions(ActorType.Manager).ShouldBeEmpty();
    }

    [Fact]
    public void Create_collects_all_product_mismatches_by_field()
    {
        var company = TestData.Company(registeredAt: new DateOnly(2026, 6, 15));
        var product = TestData.Product(minAmount: 300_000, maxAmount: 10_000_000, minTermMonths: 3, maxTermMonths: 24, allowedPurposes: [LoanPurpose.WorkingCapital], minBusinessAgeMonths: 12);

        var exception = Should.Throw<ValidationException>(() =>
            CreditApplication.Create(company, product, UserId, 50_000_000, 60, LoanPurpose.Equipment, null, TestData.Now));

        exception.Errors.Keys.ShouldBe(["amount", "termMonths", "purpose", "companyId"], ignoreOrder: true);
        exception.Errors["amount"][0].ShouldContain("300 000");
        exception.Errors["amount"][0].ShouldContain("10 000 000");
        exception.Errors["termMonths"][0].ShouldContain("от 3 до 24");
        exception.Errors["purpose"][0].ShouldContain("Пополнение оборотных средств");
        exception.Errors["companyId"][0].ShouldContain("не менее 12");
    }

    [Fact]
    public void Create_rejects_too_long_purpose_description()
    {
        var company = TestData.Company();
        var product = TestData.Product();

        var exception = Should.Throw<ValidationException>(() =>
            CreditApplication.Create(company, product, UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, new string('x', 1001), TestData.Now));

        exception.Errors.Keys.ShouldBe(["purposeDescription"]);
    }

    [Fact]
    public void Create_stores_need_by_date_and_keeps_it_optional()
    {
        var company = TestData.Company();
        var product = TestData.Product();
        var needBy = new DateOnly(2026, 11, 15);

        var withDate = CreditApplication.Create(company, product, UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now, needBy);
        var withoutDate = CreditApplication.Create(company, product, UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);

        withDate.NeedByDate.ShouldBe(needBy);
        withoutDate.NeedByDate.ShouldBeNull();
    }

    [Theory]
    [InlineData(-1, "раньше сегодняшнего дня")]
    [InlineData(731, "не дальше чем на 730 дней")]
    public void Create_rejects_need_by_date_outside_the_allowed_window(int daysFromToday, string expectedMessagePart)
    {
        var today = DateOnly.FromDateTime(TestData.Now.UtcDateTime);

        var exception = Should.Throw<ValidationException>(() =>
            CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now, today.AddDays(daysFromToday)));

        exception.Errors.Keys.ShouldBe(["needByDate"]);
        exception.Errors["needByDate"][0].ShouldContain(expectedMessagePart);
    }

    [Fact]
    public void Create_accepts_today_and_the_last_day_of_the_window_as_need_by_date()
    {
        var today = DateOnly.FromDateTime(TestData.Now.UtcDateTime);

        Should.NotThrow(() => CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now, today));
        Should.NotThrow(() => CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now, today.AddDays(730)));
    }

    [Fact]
    public void Cancel_moves_submitted_application_to_cancelled_with_reason()
    {
        var application = CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);
        var later = TestData.Now.AddMinutes(5);

        application.Cancel(UserId, "Передумали", later);

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        application.UpdatedAt.ShouldBe(later);
        application.History.Count.ShouldBe(2);
        application.History[1].FromStatus.ShouldBe(ApplicationStatus.Submitted);
        application.History[1].ToStatus.ShouldBe(ApplicationStatus.Cancelled);
        application.History[1].Comment.ShouldBe("Передумали");
        application.AvailableActions(ActorType.Client).ShouldBeEmpty();
    }

    [Fact]
    public void Cancel_twice_is_rejected()
    {
        var application = CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);
        application.Cancel(UserId, null, TestData.Now);

        Should.Throw<InvalidStatusTransitionException>(() => application.Cancel(UserId, null, TestData.Now));
    }

    [Fact]
    public void ChangeStatus_by_wrong_actor_is_rejected_and_leaves_state_untouched()
    {
        var application = CreditApplication.Create(TestData.Company(), TestData.Product(), UserId, 1_000_000, 12, LoanPurpose.WorkingCapital, null, TestData.Now);

        Should.Throw<InvalidStatusTransitionException>(() =>
            application.ChangeStatus(ApplicationStatus.Validating, ActorType.Client, UserId, null, TestData.Now));

        application.Status.ShouldBe(ApplicationStatus.Submitted);
        application.History.Count.ShouldBe(1);
    }
}
