using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Financing;
using AlfaCredit.Infrastructure.Persistence.Seeding;
using AlfaCredit.UnitTests.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Financing;

/// <summary>Расчёты предварительного подбора на данных seed. Контрольные числа из docs/financing-preview.md.</summary>
public sealed class FinancingAdvisorTests
{
    // Пятница 23.10.2026: следующий рабочий день понедельник 26.10.2026
    private static readonly DateTimeOffset Now = new(2026, 10, 23, 14, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<CreditProduct> Catalog = SeedData.CreateProducts();

    private readonly FinancingAdvisor _advisor = new(new FinancingPreviewOptions(), new DecisionPolicyOptions());

    [Fact]
    public void Romashka_working_capital_for_12_months_matches_the_contract_numbers()
    {
        var company = SeedCompanies.ByLogin("client_good");
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 11, 15), 12);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.CalculatedAt.ShouldBe(Now);
        preview.BusinessAgeMonths.ShouldBe(91);
        preview.DaysUntilNeeded.ShouldBe(23);
        preview.Options.Select(o => o.Product.Code).ShouldBe(["credit-line", "working-capital", "overdraft", "express"]);
        preview.Unsuitable.Select(u => u.Product.Code).ShouldBe(["investment"]);
        preview.Unsuitable[0].Reasons.Select(r => r.Code).ShouldBe(["purpose_not_allowed"]);
        preview.Unsuitable[0].Reasons[0].Text.ShouldContain("«Покупка оборудования или транспорта»");

        var best = preview.Options[0];
        best.IsRecommended.ShouldBeTrue();
        best.TermMonths.ShouldBe(12);
        best.TermSuggested.ShouldBeFalse();
        best.Cost.ShouldBe(new OptionCost(18.5m, 21.5m, 137_877.18m, 140_030.86m, 1_654_526.12m, 154_526.12m, 180_370.38m));
        best.Affordability.MonthlyProfit.ShouldBe(600_000);
        best.Affordability.CurrentDebtPayments.ShouldBe(150_000);
        best.Affordability.DebtLoad.ShouldBe(0.48m);
        best.Affordability.Level.ShouldBe(DebtLoadLevel.Moderate);
        best.Affordability.ProfitBasis.ShouldBe(ProfitBasis.NetProfit);
        best.Affordability.Explanation.ShouldContain("48 %");
        best.Affordability.Explanation.ShouldEndWith("заметная, но посильная нагрузка (30–50 %).");
        best.Funding.ShouldBe(best.Funding with { DecisionPath = FundingPath.Auto, BusinessDays = 1, ExpectedDate = new DateOnly(2026, 10, 26), InTime = true });
        best.Funding.Explanation.ShouldContain("26.10.2026");
        best.Funding.Explanation.ShouldContain("остаётся 20 дней");
        best.Explanations.Select(e => e.Code).ShouldBe(["product_about", "no_collateral", "auto_decision", "data_prefilled", "cheapest"]);
        best.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk"]);
        best.Limitations[0].Text.ShouldContain("от 18,5 % до 21,5 %");
        best.Requirements.Select(r => r.Code).ShouldBe(["credit_history_consent"]);

        // Оборотный кредит: те же числа, что в договоре из docs/completion-flow.md
        var workingCapital = preview.Options[1];
        workingCapital.IsRecommended.ShouldBeFalse();
        workingCapital.Cost.MonthlyPayment.ShouldBe(138_593.06m);
        workingCapital.Cost.TotalPayment.ShouldBe(1_663_116.74m);
        workingCapital.Cost.TotalInterest.ShouldBe(163_116.74m);
        workingCapital.Explanations.ShouldNotContain(e => e.Code == "cheapest");

        preview.Summary.ShouldStartWith("Подходят 4 варианта из 5. Рекомендуем «Кредитная линия»: платёж 137 877 ₽ в месяц, переплата 154 526 ₽ за 12 мес., деньги ориентировочно 26.10.2026, к нужной дате успеваем.");
        preview.Disclaimer.ShouldBe(FinancingAdvisor.Disclaimer);
    }

    [Fact]
    public void Term_is_suggested_per_product_when_not_given()
    {
        var company = SeedCompanies.ByLogin("client_good");
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 11, 15), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        // Комфортного срока (нагрузка меньше 30 %) нет; берётся самый короткий срок без штрафа в скоринге (нагрузка меньше 50 %)
        preview.Options.ShouldAllBe(o => o.TermSuggested && o.Affordability.Level == DebtLoadLevel.Moderate);
        preview.Options.ShouldAllBe(o => o.Explanations.Any(e => e.Code == "term_suggested"));
        preview.Options.Select(o => (o.Product.Code, o.TermMonths)).ShouldBe([("credit-line", 11), ("working-capital", 12), ("overdraft", 12), ("express", 12)]);
        preview.Options.ShouldAllBe(o => o.Affordability.DebtLoad <= 0.50m, "нагрузка в ответе округлена до сотых: 0.4988 показывается как 0.50");
        preview.Options[1].Cost.MonthlyPayment.ShouldBe(138_593.06m, "12 месяцев: тот же платёж, что в демо-заявке");
        preview.Options[0].Explanations.Single(e => e.Code == "term_suggested").Text.ShouldContain("от 6 до 36 мес.");
    }

    [Fact]
    public void Young_business_gets_reasons_instead_of_options()
    {
        var company = SeedCompanies.ByLogin("client_new");
        var query = new FinancingPreviewQuery(company.Id, 500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.BusinessAgeMonths.ShouldBe(4);
        preview.Options.ShouldBeEmpty();
        preview.Unsuitable.Count.ShouldBe(5);
        preview.Unsuitable.ShouldAllBe(u => u.Reasons.Any(r => r.Code == "business_too_young"));
        preview.Unsuitable.Single(u => u.Product.Code == "express").Reasons.Single().Text.ShouldBe("Возраст бизнеса 4 мес., для продукта требуется не менее 6 мес.");
        preview.Unsuitable.Single(u => u.Product.Code == "investment").Reasons.Select(r => r.Code).ShouldBe(["purpose_not_allowed", "amount_below_min", "business_too_young"]);
        preview.Summary.ShouldStartWith("Подходящих продуктов нет.");
        preview.Summary.ShouldContain("не менее 6 мес. Продукты: «Экспресс-кредит», «Овердрафт».");
        preview.Summary.ShouldContain("не менее 12 мес. Продукты: «Оборотный кредит», «Кредитная линия».");
        preview.Summary.ShouldContain("Минимальная сумма продукта 1 000 000 ₽. Продукт: «Инвестиционный кредит».");
    }

    [Fact]
    public void Collateral_product_goes_to_manager_and_misses_a_close_deadline()
    {
        var company = SeedCompanies.ByLogin("client_manual");
        var query = new FinancingPreviewQuery(company.Id, 5_000_000, LoanPurpose.Equipment, new DateOnly(2026, 11, 2), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.Options.Select(o => o.Product.Code).ShouldBe(["credit-line", "investment"]);
        preview.Unsuitable.Select(u => (u.Product.Code, u.Reasons[0].Code)).ShouldBe(
            [("working-capital", "purpose_not_allowed"), ("express", "amount_above_max"), ("overdraft", "purpose_not_allowed")]);

        var creditLine = preview.Options[0];
        creditLine.IsRecommended.ShouldBeTrue();
        creditLine.TermMonths.ShouldBe(36, "нагрузка остаётся высокой на любом сроке, берётся максимальный");
        creditLine.Funding.DecisionPath.ShouldBe(FundingPath.Auto, "5 000 000 ₽ равно лимиту автоодобрения, а не выше него");
        creditLine.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        creditLine.Affordability.DebtLoad.ShouldBe(1.04m);
        creditLine.Affordability.MonthlyProfit.ShouldBe(1_333_333.33m);
        creditLine.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "high_debt_load"]);

        var investment = preview.Options[1];
        investment.TermMonths.ShouldBe(84);
        investment.Cost.InterestRate.ShouldBe(17.5m);
        investment.Cost.MonthlyPayment.ShouldBe(103_628.97m);
        investment.Cost.TotalInterest.ShouldBe(3_704_833.32m);
        investment.Funding.ShouldBe(investment.Funding with { DecisionPath = FundingPath.ManualReview, BusinessDays = 10, ExpectedDate = new DateOnly(2026, 11, 6), InTime = false });
        investment.Funding.Explanation.ShouldStartWith("Нужен залог, решение принимает менеджер: деньги ориентировочно 06.11.2026, это позже нужной даты 02.11.2026.");
        investment.Explanations.Select(e => e.Code).ShouldBe(["product_about", "term_suggested", "data_prefilled"]);
        investment.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "manual_review_amount", "collateral_required", "high_debt_load", "deadline_missed"]);
        investment.Requirements.Select(r => r.Code).ShouldBe(["credit_history_consent", "collateral_documents"]);
    }

    [Fact]
    public void Amount_above_auto_limit_is_reviewed_by_manager_within_three_business_days()
    {
        var company = SeedCompanies.ByLogin("client_manual");
        var query = new FinancingPreviewQuery(company.Id, 8_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 10, 26), 24);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.Options.Select(o => o.Product.Code).ShouldBe(["credit-line", "working-capital"]);
        preview.Options.ShouldAllBe(o => o.Funding.DecisionPath == FundingPath.ManualReview && o.Funding.BusinessDays == 3 && !o.Funding.InTime);
        preview.Options.ShouldAllBe(o => o.Funding.ExpectedDate == new DateOnly(2026, 10, 28));
        preview.Options[1].Cost.MonthlyPayment.ShouldBe(405_215.01m, "тот же платёж, что в запросе к скорингу для client_manual");
        preview.Options[1].Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "manual_review_amount", "high_debt_load", "deadline_missed"]);
        preview.Options[1].Explanations.ShouldNotContain(e => e.Code == "auto_decision");
        preview.Unsuitable.Single(u => u.Product.Code == "express").Reasons.Select(r => r.Code).ShouldBe(["amount_above_max", "term_out_of_range"]);
        preview.Summary.ShouldEndWith("это позже нужной даты.");
    }

    [Fact]
    public void Options_that_make_the_deadline_rank_above_cheaper_ones_that_do_not()
    {
        var company = SeedCompanies.ByLogin("client_good");
        var cheapButSlow = TestData.Product(code: "cheap-collateral", baseRate: 10m, requiresCollateral: true, allowedPurposes: [LoanPurpose.WorkingCapital]);
        var fastButPricey = TestData.Product(code: "fast", baseRate: 25m, allowedPurposes: [LoanPurpose.WorkingCapital]);
        var query = new FinancingPreviewQuery(company.Id, 1_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 10, 27), 12);

        var preview = _advisor.Build(company, [cheapButSlow, fastButPricey], query, Now);

        preview.Options.Select(o => o.Product.Code).ShouldBe(["fast", "cheap-collateral"]);
        preview.Options[0].IsRecommended.ShouldBeTrue();
        preview.Options[0].Explanations.ShouldNotContain(e => e.Code == "cheapest");
        preview.Options[1].Explanations.ShouldContain(e => e.Code == "cheapest");
        preview.Options[1].Funding.InTime.ShouldBeFalse();
    }

    [Fact]
    public void Amount_above_a_quarter_of_revenue_warns_that_it_may_be_reduced()
    {
        var company = SeedCompanies.ByLogin("client_medium");
        var query = new FinancingPreviewQuery(company.Id, 3_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 24);

        var preview = _advisor.Build(company, Catalog, query, Now);

        var option = preview.Options.Single(o => o.Product.Code == "working-capital");
        option.Cost.MonthlyPayment.ShouldBe(151_955.63m);
        option.Affordability.Level.ShouldBe(DebtLoadLevel.Moderate);
        var reduced = option.Limitations.Single(l => l.Code == "amount_may_be_reduced");
        reduced.Text.ShouldContain("2 400 000 ₽");
        reduced.Text.ShouldContain("25 %");
    }

    [Fact]
    public void Missing_financial_statements_are_listed_as_a_requirement_for_large_amounts_and_credit_lines()
    {
        var company = CompanyWithoutStatements(registeredAt: new DateOnly(2020, 1, 10), annualRevenue: 20_000_000);
        var query = new FinancingPreviewQuery(company.Id, 4_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 12);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.Options.Select(o => o.Product.Code).ShouldBe(["credit-line", "working-capital", "overdraft"]);
        preview.Options.ShouldAllBe(o => o.Requirements.Any(r => r.Code == "financials_required"));
        preview.Options.ShouldAllBe(o => o.Affordability.ProfitBasis == ProfitBasis.RevenueEstimate);
        preview.Options[0].Affordability.MonthlyProfit.ShouldBe(166_666.67m, "10 % выручки в месяц");
        preview.Options[0].Affordability.Explanation.ShouldContain("принята как 10 % выручки");

        var small = _advisor.Build(company, Catalog, query with { Amount = 1_000_000 }, Now);
        small.Options.Single(o => o.Product.Code == "working-capital").Requirements.ShouldNotContain(r => r.Code == "financials_required");
        small.Options.Single(o => o.Product.Code == "credit-line").Requirements.ShouldContain(r => r.Code == "financials_required");
    }

    [Theory]
    [InlineData(7_200_000, 150_000, 0.48, DebtLoadLevel.Moderate)]
    [InlineData(60_000_000, 0, 0.03, DebtLoadLevel.Comfortable)]
    [InlineData(5_400_000, 150_000, 0.64, DebtLoadLevel.Elevated)]
    [InlineData(100_000, 600_000, 9.99, DebtLoadLevel.High)]
    public void Debt_load_levels_follow_the_scoring_bands(decimal netProfit, decimal monthlyDebtPayments, decimal expectedLoad, DebtLoadLevel expectedLevel)
    {
        var company = TestData.Company(netProfit: netProfit, monthlyDebtPayments: monthlyDebtPayments, annualRevenue: 100_000_000);
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 12);

        var option = _advisor.Build(company, [TestData.Product()], query, Now).Options.Single();

        option.Affordability.DebtLoad.ShouldBe(expectedLoad);
        option.Affordability.Level.ShouldBe(expectedLevel);
        option.Limitations.Any(l => l.Code == "high_debt_load").ShouldBe(expectedLevel == DebtLoadLevel.High);
        option.Limitations.Any(l => l.Code == "elevated_debt_load").ShouldBe(expectedLevel == DebtLoadLevel.Elevated);
    }

    [Fact]
    public void Expenses_above_ninety_percent_of_revenue_are_a_limitation()
    {
        var zakat = SeedCompanies.ByLogin("client_bad");
        var romashka = SeedCompanies.ByLogin("client_good");
        var query = new FinancingPreviewQuery(zakat.Id, 500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 6);

        var withPenalty = _advisor.Build(zakat, Catalog, query, Now);
        var without = _advisor.Build(romashka, Catalog, query with { CompanyId = romashka.Id }, Now);

        withPenalty.Options.ShouldNotBeEmpty();
        withPenalty.Options.ShouldAllBe(o => o.Limitations.Any(l => l.Code == "expense_ratio_high"));
        withPenalty.Options[0].Limitations.Single(l => l.Code == "expense_ratio_high").Text.ShouldContain("99 %");
        without.Options.ShouldAllBe(o => o.Limitations.All(l => l.Code != "expense_ratio_high"));
    }

    [Fact]
    public void No_profit_means_undefined_load_and_high_level()
    {
        var company = TestData.Company(netProfit: -500_000, annualRevenue: 20_000_000);
        var query = new FinancingPreviewQuery(company.Id, 1_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), null);

        var option = _advisor.Build(company, [TestData.Product()], query, Now).Options.Single();

        option.Affordability.DebtLoad.ShouldBeNull();
        option.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        option.Affordability.Explanation.ShouldStartWith("По данным компании прибыли за последние 12 месяцев нет");
        option.TermMonths.ShouldBe(24, "без прибыли подбирается максимальный срок продукта");
        option.Limitations.Single(l => l.Code == "high_debt_load").Text.ShouldBe("По данным компании прибыли нет: возможен отказ или снижение суммы.");
    }

    [Fact]
    public void Expenses_without_net_profit_estimate_profit_as_revenue_minus_expenses()
    {
        var company = Company.Create("7701234567", "1157746123456", "ООО «Ромашка»", LegalForm.Ooo, "47.11", new DateOnly(2019, 3, 12), "Москва", "Иванова М. С.",
            TaxRegime.Osno, 48_000_000, 40_800_000, null, 150_000, 24, Now);
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 12);

        var option = _advisor.Build(company, [TestData.Product()], query, Now).Options.Single();

        option.Affordability.ProfitBasis.ShouldBe(ProfitBasis.RevenueMinusExpenses);
        option.Affordability.MonthlyProfit.ShouldBe(600_000);
        option.Affordability.Explanation.ShouldEndWith("Прибыль оценена как выручка минус расходы.");
    }

    [Fact]
    public void Every_option_passes_application_validation_with_the_same_parameters()
    {
        var company = SeedCompanies.ByLogin("client_medium");
        var query = new FinancingPreviewQuery(company.Id, 2_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.Options.ShouldNotBeEmpty();
        foreach (var option in preview.Options)
        {
            Should.NotThrow(() => Core.Domain.Applications.CreditApplication.Create(
                company, option.Product, Guid.NewGuid(), option.Amount, option.TermMonths, query.Purpose, null, Now, query.NeedByDate));
        }
    }

    private static Company CompanyWithoutStatements(DateOnly registeredAt, decimal annualRevenue) =>
        Company.Create("772812345678", "326770000054321", "ИП Петрова Елена Викторовна", LegalForm.Ip, "96.02", registeredAt, "Москва", "Петрова Е. В.",
            TaxRegime.Patent, annualRevenue, null, null, 0, 2, Now);
}
