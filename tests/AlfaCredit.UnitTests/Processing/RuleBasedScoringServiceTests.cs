using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.UnitTests.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class RuleBasedScoringServiceTests
{
    [Fact]
    public void Stable_company_with_clean_checks_gets_grade_A()
    {
        var outcome = RuleBasedScoringService.Score(Request(amount: 1_500_000, termMonths: 12));

        outcome.Score.ShouldBe(95);
        outcome.Grade.ShouldBe(RiskGrade.A);
        outcome.Source.ShouldBe(ScoringSource.Rules);
        outcome.Factors.Select(f => f.Code).ShouldContain("credit_history");
        outcome.Factors.ShouldAllBe(f => !string.IsNullOrWhiteSpace(f.ClientExplanation));
        outcome.Warnings.ShouldBeEmpty();
    }

    [Fact]
    public void High_debt_load_and_overdues_drop_the_grade()
    {
        var checks = new ScoringChecks(
            new EgrulResult(EgrulStatus.Active, false, false, new DateOnly(2020, 2, 14), "56.10"),
            new FnsResult(700_000, true, 13_200_000),
            new CreditBureauResult(48, 4, 1, 9_000_000, 600_000, 95, 3, false),
            new FsspResult(2, 1_400_000));

        var outcome = RuleBasedScoringService.Score(Request(amount: 2_000_000, termMonths: 12, revenue: 14_000_000, netProfit: 100_000, monthlyDebtPayments: 600_000, checks: checks));

        outcome.Grade.ShouldBe(RiskGrade.E);
        outcome.Factors.First(f => f.Code == "credit_history").Impact.ShouldBe(-30);
        outcome.Factors.First(f => f.Code == "tax_debt").Impact.ShouldBe(-30);
    }

    [Fact]
    public void Missing_sources_produce_warnings_instead_of_failing()
    {
        var outcome = RuleBasedScoringService.Score(Request(amount: 1_000_000, termMonths: 12, netProfit: null, checks: new ScoringChecks(null, null, null, null)));

        outcome.Warnings.ShouldBe(["net_profit_missing", "credit_bureau_missing", "fns_missing", "fssp_missing"], ignoreOrder: true);
        outcome.Score.ShouldBeInRange(0, 100);
    }

    [Fact]
    public void Factors_are_ordered_by_absolute_impact()
    {
        var outcome = RuleBasedScoringService.Score(Request(amount: 1_500_000, termMonths: 12));

        var impacts = outcome.Factors.Select(f => Math.Abs(f.Impact)).ToArray();
        impacts.ShouldBe(impacts.OrderByDescending(i => i).ToArray());
    }

    internal static ScoringRequest Request(
        decimal amount,
        int termMonths,
        decimal revenue = 48_000_000,
        decimal? netProfit = 7_200_000,
        decimal monthlyDebtPayments = 150_000,
        ScoringChecks? checks = null)
    {
        var company = TestData.Company(annualRevenue: revenue, netProfit: netProfit, monthlyDebtPayments: monthlyDebtPayments);
        var product = TestData.Product();
        var application = Core.Domain.Applications.CreditApplication.Create(company, product, Guid.NewGuid(), amount, termMonths, Core.Domain.Products.LoanPurpose.WorkingCapital, null, TestData.Now);
        foreach (var check in ChecksFrom(application.Id, checks ?? DefaultChecks()))
        {
            application.RecordCheck(check);
        }

        return Core.Features.Processing.ApplicationPipeline.BuildScoringRequest(application, new Core.Abstractions.CompanyApplicationHistory(0, 0, 0), TestData.Now);
    }

    internal static ScoringChecks DefaultChecks() => new(
        new EgrulResult(EgrulStatus.Active, false, false, new DateOnly(2019, 3, 12), "47.11"),
        new FnsResult(0, false, 47_300_000),
        new CreditBureauResult(60, 1, 3, 1_200_000, 150_000, 0, 0, false),
        new FsspResult(0, 0));

    private static IEnumerable<ExternalCheck> ChecksFrom(Guid applicationId, ScoringChecks checks)
    {
        if (checks.Egrul is { } e)
        {
            yield return ExternalCheck.Succeeded(applicationId, CheckSource.Egrul, e, "ok", [], 1, TestData.Now);
        }

        if (checks.Fns is { } f)
        {
            yield return ExternalCheck.Succeeded(applicationId, CheckSource.Fns, f, "ok", [], 1, TestData.Now);
        }

        if (checks.CreditBureau is { } b)
        {
            yield return ExternalCheck.Succeeded(applicationId, CheckSource.CreditBureau, b, "ok", [], 1, TestData.Now);
        }

        if (checks.Fssp is { } s)
        {
            yield return ExternalCheck.Succeeded(applicationId, CheckSource.Fssp, s, "ok", [], 1, TestData.Now);
        }
    }
}
