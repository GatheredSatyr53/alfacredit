using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.UnitTests.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class DecisionPolicyTests
{
    private readonly DecisionPolicy _policy = new(new DecisionPolicyOptions());

    [Fact]
    public void Hard_stops_reject_regardless_of_score()
    {
        var application = Application(1_000_000, 12);
        var checks = new ChecksSnapshot(
            new EgrulResult(EgrulStatus.Active, false, true, new DateOnly(2020, 2, 14), "56.10"),
            new FnsResult(700_000, true, null),
            new CreditBureauResult(48, 4, 1, 9_000_000, 600_000, 95, 3, false),
            new FsspResult(2, 1_400_000));

        var outcome = _policy.Decide(application, checks, Scoring(application, 90, RiskGrade.A));

        outcome.Type.ShouldBe(DecisionType.Rejected);
        outcome.Reasons.Select(r => r.Code).ShouldBe(["tax_debt", "accounts_blocked", "overdue_90", "enforcement_debt"], ignoreOrder: true);
    }

    [Fact]
    public void Grade_A_within_auto_limit_is_approved_as_requested()
    {
        var application = Application(1_500_000, 12);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 95, RiskGrade.A));

        outcome.Type.ShouldBe(DecisionType.Approved);
        outcome.Amount.ShouldBe(1_500_000);
        outcome.TermMonths.ShouldBe(12);
        outcome.InterestRate.ShouldBe(19.5m);
        outcome.MonthlyPayment.ShouldNotBeNull();
        outcome.MonthlyPayment!.Value.ShouldBeInRange(138_000, 139_500);
    }

    [Fact]
    public void Grade_C_is_approved_with_reduced_amount_and_rate_premium()
    {
        var application = Application(3_000_000, 24, revenue: 9_600_000);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 55, RiskGrade.C));

        outcome.Type.ShouldBe(DecisionType.ApprovedWithConditions);
        outcome.Amount.ShouldBe(2_400_000);
        outcome.InterestRate.ShouldBe(22.5m);
        outcome.Conditions.ShouldNotBeNullOrWhiteSpace();
        outcome.Reasons.Select(r => r.Code).ShouldContain("amount_reduced");
    }

    [Fact]
    public void Grade_D_goes_to_manual_review_with_recommendation()
    {
        var application = Application(1_000_000, 12);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 40, RiskGrade.D));

        outcome.Type.ShouldBe(DecisionType.ManualReview);
        outcome.RecommendedType.ShouldBe(DecisionType.ApprovedWithConditions);
        outcome.Reasons[0].Code.ShouldBe("grade_d");
    }

    [Fact]
    public void Grade_E_is_rejected()
    {
        var application = Application(1_000_000, 12);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 20, RiskGrade.E));

        outcome.Type.ShouldBe(DecisionType.Rejected);
        outcome.Reasons[0].Code.ShouldBe("grade_e");
    }

    [Fact]
    public void Amount_above_auto_limit_requires_manager_even_for_grade_A()
    {
        var application = Application(8_000_000, 24, revenue: 180_000_000);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 85, RiskGrade.A));

        outcome.Type.ShouldBe(DecisionType.ManualReview);
        outcome.RecommendedType.ShouldBe(DecisionType.Approved);
        outcome.Reasons[0].Code.ShouldBe("amount_above_auto_limit");
    }

    [Fact]
    public void Missing_critical_checks_require_manager()
    {
        var application = Application(1_000_000, 12);
        var checks = CleanChecks() with { CreditBureau = null };

        var outcome = _policy.Decide(application, checks, Scoring(application, 85, RiskGrade.A));

        outcome.Type.ShouldBe(DecisionType.ManualReview);
        outcome.Reasons[0].Code.ShouldBe("checks_missing");
    }

    [Fact]
    public void Collateral_products_require_manager()
    {
        var product = TestData.Product(code: "investment", minAmount: 1_000_000, maxAmount: 50_000_000, minTermMonths: 12, maxTermMonths: 84, allowedPurposes: [LoanPurpose.Equipment], minBusinessAgeMonths: 24, requiresCollateral: true, autoApprovalLimit: 3_000_000);
        var application = CreditApplication.Create(TestData.Company(), product, Guid.NewGuid(), 2_000_000, 36, LoanPurpose.Equipment, null, TestData.Now);

        var outcome = _policy.Decide(application, CleanChecks(), Scoring(application, 90, RiskGrade.A));

        outcome.Type.ShouldBe(DecisionType.ManualReview);
        outcome.Reasons[0].Code.ShouldBe("collateral_required");
    }

    private static CreditApplication Application(decimal amount, int termMonths, decimal revenue = 48_000_000) =>
        CreditApplication.Create(TestData.Company(annualRevenue: revenue), TestData.Product(), Guid.NewGuid(), amount, termMonths, LoanPurpose.WorkingCapital, null, TestData.Now);

    private static ChecksSnapshot CleanChecks() => new(
        new EgrulResult(EgrulStatus.Active, false, false, new DateOnly(2019, 3, 12), "47.11"),
        new FnsResult(0, false, 47_300_000),
        new CreditBureauResult(60, 1, 3, 1_200_000, 150_000, 0, 0, false),
        new FsspResult(0, 0));

    private static ScoringResult Scoring(CreditApplication application, int score, RiskGrade grade) =>
        ScoringResult.Create(
            application.Id,
            Guid.CreateVersion7(),
            new ScoringOutcome(score, grade, 0.05m, [new ScoringFactor("debt_load", "Долговая нагрузка", -5, "0.5", "Платежи занимают половину прибыли.")], [], [], "rules-v1", ScoringSource.Rules),
            durationMs: 0,
            TestData.Now);
}
