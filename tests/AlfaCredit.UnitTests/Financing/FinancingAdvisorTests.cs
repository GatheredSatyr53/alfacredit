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
    // Пятница 23.10.2026 17:00 по Москве: следующий рабочий день понедельник 26.10.2026
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
        preview.CalculationDate.ShouldBe(new DateOnly(2026, 10, 23));
        preview.BusinessAgeMonths.ShouldBe(91);
        preview.DaysUntilNeeded.ShouldBe(23);
        preview.Options.Select(o => o.Product.Code).ShouldBe(["working-capital", "express"]);
        preview.NotCompared.Select(n => n.Product.Code).ShouldBe(["overdraft", "credit-line"]);
        preview.NotCompared.ShouldAllBe(n => n.Reason.Code == "limit_product");
        preview.NotCompared[1].Reason.Text.ShouldContain("оформляется отдельной заявкой");
        preview.Unsuitable.Select(u => u.Product.Code).ShouldBe(["investment"]);
        preview.Unsuitable[0].Reasons.Select(r => r.Code).ShouldBe(["purpose_not_allowed"]);
        preview.Unsuitable[0].SuggestedAmount.ShouldBeNull();

        var best = preview.Options[0];
        best.IsRecommended.ShouldBeTrue();
        best.TermMonths.ShouldBe(12);
        best.TermSuggested.ShouldBeFalse();
        // Те же числа, что в договоре из docs/completion-flow.md
        best.Cost.ShouldBe(new OptionCost(19.5m, 22.5m, 138_593.06m, 140_752.78m, 1_663_116.74m, 163_116.74m, 189_033.30m));
        best.Affordability.MonthlyProfit.ShouldBe(600_000);
        best.Affordability.CurrentDebtPayments.ShouldBe(150_000);
        best.Affordability.DebtLoad.ShouldBe(0.48m);
        best.Affordability.Level.ShouldBe(DebtLoadLevel.Moderate);
        best.Affordability.ProfitBasis.ShouldBe(ProfitBasis.NetProfit);
        best.Affordability.Explanation.ShouldEndWith("заметная, но посильная нагрузка (30–50 %).");
        best.Funding.ShouldBe(best.Funding with { DecisionPath = FundingPath.Auto, BusinessDays = 1, ExpectedDate = new DateOnly(2026, 10, 26), InTime = true });
        best.Funding.Explanation.ShouldStartWith("Сумма в пределах лимита автоматического решения 5 000 000 ₽: при чистых проверках и хорошей оценке решение принимается без менеджера. При одобрении и подписании договора деньги ориентировочно 26.10.2026, до нужной даты 15.11.2026 остаётся 20 дней.");
        best.Funding.Explanation.ShouldEndWith("Ориентир не является одобрением.");
        best.SuggestedAmount.ShouldBeNull("сумма в лимитах и нагрузка посильная");
        best.Explanations.Select(e => e.Code).ShouldBe(["product_about", "no_collateral", "auto_decision_possible", "data_prefilled", "checks_automatic", "cheapest"]);
        best.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk"]);
        best.Requirements.ShouldBeEmpty("данные компании полные, залога нет, отдельных согласий в кабинете не реализовано");

        preview.Options[1].Product.Code.ShouldBe("express");
        preview.Options[1].IsRecommended.ShouldBeFalse();
        preview.Options[1].Explanations.ShouldNotContain(e => e.Code == "cheapest");
        preview.Summary.ShouldBe("Подходят 2 варианта из 3 сравниваемых продуктов. Рекомендуем «Оборотный кредит»: платёж 138 593 ₽ в месяц, переплата 163 117 ₽ за 12 мес., при одобрении деньги ориентировочно 26.10.2026, к нужной дате успеваем.");
        preview.Disclaimer.ShouldContain("не является одобрением и не гарантирует выдачу");
    }

    [Fact]
    public void Today_is_the_moscow_date_for_days_business_age_and_funding()
    {
        var company = SeedCompanies.ByLogin("client_good");
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 11, 15), 12);
        // 23.10.2026 21:30 UTC: в Москве уже суббота 24.10.2026
        var lateEvening = new DateTimeOffset(2026, 10, 23, 21, 30, 0, TimeSpan.Zero);

        var preview = _advisor.Build(company, Catalog, query, lateEvening);

        preview.CalculationDate.ShouldBe(new DateOnly(2026, 10, 24));
        preview.DaysUntilNeeded.ShouldBe(22);
        preview.Options[0].Funding.ExpectedDate.ShouldBe(new DateOnly(2026, 10, 26), "суббота + 1 рабочий день = понедельник");
    }

    [Fact]
    public void Term_is_suggested_per_product_when_not_given()
    {
        var company = SeedCompanies.ByLogin("client_good");
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 11, 15), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        // Комфортного срока (нагрузка меньше 30 %) нет; берётся самый короткий срок без штрафа в скоринге (нагрузка меньше 50 %)
        preview.Options.Select(o => (o.Product.Code, o.TermMonths, o.TermSuggested)).ShouldBe([("working-capital", 12, true), ("express", 12, true)]);
        preview.Options.ShouldAllBe(o => o.Affordability.Level == DebtLoadLevel.Moderate);
        preview.Options[0].Explanations.Single(e => e.Code == "term_suggested").Text
            .ShouldBe("Срок 12 мес.: самый короткий срок без штрафа за нагрузку в оценке риска; можно выбрать от 3 до 24 мес.");
        preview.Options[0].Cost.MonthlyPayment.ShouldBe(138_593.06m);
    }

    [Fact]
    public void Young_business_gets_reasons_instead_of_options()
    {
        var company = SeedCompanies.ByLogin("client_new");
        var query = new FinancingPreviewQuery(company.Id, 500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.BusinessAgeMonths.ShouldBe(4);
        preview.Options.ShouldBeEmpty();
        preview.NotCompared.Count.ShouldBe(2);
        preview.Unsuitable.Select(u => u.Product.Code).ShouldBe(["working-capital", "express", "investment"]);
        preview.Unsuitable.ShouldAllBe(u => u.Reasons.Any(r => r.Code == "business_too_young") && u.SuggestedAmount == null);
        preview.Unsuitable.Single(u => u.Product.Code == "express").Reasons.Single().Text.ShouldBe("Возраст бизнеса 4 мес., для продукта требуется не менее 6 мес.");
        preview.Summary.ShouldStartWith("Подходящих продуктов нет.");
        preview.Summary.ShouldContain("не менее 6 мес. Продукт: «Экспресс-кредит».");
        preview.Summary.ShouldEndWith("Продукты с лимитом («Овердрафт», «Кредитная линия») в сравнение не входят и оформляются отдельной заявкой.");
    }

    [Fact]
    public void Collateral_product_goes_to_manager_and_misses_a_close_deadline()
    {
        var company = SeedCompanies.ByLogin("client_manual");
        var query = new FinancingPreviewQuery(company.Id, 5_000_000, LoanPurpose.Equipment, new DateOnly(2026, 11, 2), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        preview.Options.Select(o => o.Product.Code).ShouldBe(["investment"]);
        preview.NotCompared.Select(n => n.Product.Code).ShouldBe(["overdraft", "credit-line"]);
        preview.Unsuitable.Select(u => (u.Product.Code, u.Reasons[0].Code)).ShouldBe([("working-capital", "purpose_not_allowed"), ("express", "amount_above_max")]);

        var express = preview.Unsuitable.Single(u => u.Product.Code == "express");
        express.SuggestedAmount.ShouldNotBeNull();
        express.SuggestedAmount.Amount.ShouldBe(3_000_000);
        express.SuggestedAmount.Basis.ShouldBe(AmountSuggestionBasis.ProductMaximum);
        preview.Unsuitable.Single(u => u.Product.Code == "working-capital").SuggestedAmount.ShouldBeNull("мешает цель, а не сумма");

        var investment = preview.Options[0];
        investment.IsRecommended.ShouldBeTrue();
        investment.TermMonths.ShouldBe(84, "нагрузка остаётся высокой на любом сроке, берётся максимальный");
        investment.Explanations.Single(e => e.Code == "term_suggested").Text.ShouldStartWith("Срок 84 мес. максимальный для продукта: нагрузка остаётся высокой при любом сроке");
        investment.Cost.InterestRate.ShouldBe(17.5m);
        investment.Cost.MonthlyPayment.ShouldBe(103_628.97m);
        investment.Cost.TotalInterest.ShouldBe(3_704_833.32m);
        investment.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        investment.Affordability.DebtLoad.ShouldBe(0.98m);
        investment.Funding.ShouldBe(investment.Funding with { DecisionPath = FundingPath.ManualReview, BusinessDays = 10, ExpectedDate = new DateOnly(2026, 11, 6), InTime = false });
        investment.Funding.Explanation.ShouldStartWith("Нужен залог, заявку рассматривает кредитный менеджер. При одобрении, оформлении залога и подписании договора деньги ориентировочно 06.11.2026, это позже нужной даты 02.11.2026.");
        investment.SuggestedAmount.ShouldBeNull("залог исключает лимит автоматики, выручка не ограничивает, а посильной суммы при текущих платежах нет");
        investment.Explanations.Select(e => e.Code).ShouldBe(["product_about", "term_suggested", "data_prefilled", "checks_automatic", "cheapest"]);
        investment.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "manual_review_amount", "collateral_required", "high_debt_load", "deadline_missed"]);
        investment.Requirements.Select(r => r.Code).ShouldBe(["collateral_documents"]);
        preview.Summary.ShouldEndWith("это позже нужной даты.");
    }

    [Fact]
    public void Amount_above_auto_limit_suggests_the_limit_for_recalculation()
    {
        var company = SeedCompanies.ByLogin("client_manual");
        var query = new FinancingPreviewQuery(company.Id, 8_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 10, 26), 24);

        var preview = _advisor.Build(company, Catalog, query, Now);

        var option = preview.Options.ShouldHaveSingleItem();
        option.Product.Code.ShouldBe("working-capital");
        option.Funding.ShouldBe(option.Funding with { DecisionPath = FundingPath.ManualReview, BusinessDays = 3, ExpectedDate = new DateOnly(2026, 10, 28), InTime = false });
        option.Cost.MonthlyPayment.ShouldBe(405_215.01m, "тот же платёж, что в запросе к скорингу для client_manual");
        option.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        option.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "manual_review_amount", "high_debt_load", "deadline_missed"]);
        option.Explanations.ShouldNotContain(e => e.Code == "auto_decision_possible");
        option.SuggestedAmount.ShouldNotBeNull();
        option.SuggestedAmount.Amount.ShouldBe(5_000_000);
        option.SuggestedAmount.Basis.ShouldBe(AmountSuggestionBasis.AutoApprovalLimit, "выручка 180 млн не ограничивает, а посильной суммы при платежах 1,2 млн нет");
        option.SuggestedAmount.Text.ShouldContain("5 000 000 ₽");
        preview.Unsuitable.Single(u => u.Product.Code == "express").Reasons.Select(r => r.Code).ShouldBe(["amount_above_max", "term_out_of_range"]);
        preview.Unsuitable.Single(u => u.Product.Code == "express").SuggestedAmount.ShouldBeNull("мешает не только сумма");
    }

    [Fact]
    public void Amount_above_a_quarter_of_revenue_suggests_the_policy_cap()
    {
        var company = SeedCompanies.ByLogin("client_medium");
        var query = new FinancingPreviewQuery(company.Id, 3_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 24);

        var preview = _advisor.Build(company, Catalog, query, Now);

        var option = preview.Options.Single(o => o.Product.Code == "working-capital");
        option.Cost.MonthlyPayment.ShouldBe(151_955.63m);
        option.Affordability.Level.ShouldBe(DebtLoadLevel.Moderate);
        option.Limitations.Single(l => l.Code == "amount_may_be_reduced").Text.ShouldContain("2 400 000 ₽");
        option.SuggestedAmount.ShouldNotBeNull();
        option.SuggestedAmount.Amount.ShouldBe(2_400_000);
        option.SuggestedAmount.Basis.ShouldBe(AmountSuggestionBasis.RevenueShare);
        option.SuggestedAmount.Text.ShouldContain("25 %");
    }

    [Fact]
    public void Elevated_debt_load_suggests_the_largest_amount_without_a_scoring_penalty()
    {
        var company = TestData.Company(netProfit: 5_400_000, monthlyDebtPayments: 150_000, annualRevenue: 48_000_000);
        var query = new FinancingPreviewQuery(company.Id, 1_500_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 12);

        var option = _advisor.Build(company, [TestData.Product()], query, Now).Options.Single();

        option.Affordability.Level.ShouldBe(DebtLoadLevel.Elevated);
        option.Affordability.DebtLoad.ShouldBe(0.64m);
        option.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "elevated_debt_load"]);
        option.SuggestedAmount.ShouldNotBeNull();
        option.SuggestedAmount.Basis.ShouldBe(AmountSuggestionBasis.DebtLoad);
        option.SuggestedAmount.Amount.ShouldBe(810_000, "самая большая сумма с шагом 10 000 ₽, при которой нагрузка меньше 50 %");
        option.SuggestedAmount.Amount.ShouldBeLessThan(query.Amount);
        var payment = Core.Common.LoanMath.MonthlyPayment(option.SuggestedAmount.Amount, 19.5m, 12);
        ((150_000 + payment) / 450_000m).ShouldBeLessThan(0.50m);
        ((150_000 + Core.Common.LoanMath.MonthlyPayment(option.SuggestedAmount.Amount + 10_000, 19.5m, 12)) / 450_000m).ShouldBeGreaterThanOrEqualTo(0.50m, "следующий шаг уже со штрафом");
    }

    [Fact]
    public void Suggestion_is_the_smallest_binding_limit_and_never_below_the_product_minimum()
    {
        var company = TestData.Company(netProfit: 1_200_000, monthlyDebtPayments: 50_000, annualRevenue: 2_000_000);
        var product = TestData.Product(minAmount: 500_000, autoApprovalLimit: 900_000);
        var query = new FinancingPreviewQuery(company.Id, 1_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), 12);

        var option = _advisor.Build(company, [product], query, Now).Options.Single();

        // Границы: автоматика 900 000, доля выручки max(500 000, 25 % × 2 млн) = 500 000, нагрузка 0 (прибыль 100 000 в месяц: платёж уже выше 50 %) → ниже минимума
        option.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        option.SuggestedAmount.ShouldNotBeNull();
        option.SuggestedAmount.Amount.ShouldBe(500_000);
        option.SuggestedAmount.Basis.ShouldBe(AmountSuggestionBasis.RevenueShare);

        // Сумма в лимитах, но прибыль 20 000 ₽ в месяц: посильная сумма около 100 000 ₽, ниже минимума продукта → предложения нет
        var tooSmall = _advisor.Build(TestData.Company(netProfit: 240_000, monthlyDebtPayments: 0, annualRevenue: 48_000_000), [product], query with { Amount = 800_000 }, Now).Options.Single();
        tooSmall.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        tooSmall.SuggestedAmount.ShouldBeNull("ни одна граница не выше минимума продукта 500 000 ₽");
    }

    [Fact]
    public void Insufficient_financials_give_no_load_estimate_no_amount_suggestion_and_the_maximum_term()
    {
        var company = CompanyWithoutStatements(registeredAt: new DateOnly(2020, 1, 10), annualRevenue: 20_000_000);
        var query = new FinancingPreviewQuery(company.Id, 1_000_000, LoanPurpose.WorkingCapital, new DateOnly(2026, 12, 1), null);

        var preview = _advisor.Build(company, Catalog, query, Now);

        // При максимальных сроках экспресс-кредит на 18 мес. дешевле оборотного на 24 мес., поэтому идёт первым
        preview.Options.Select(o => o.Product.Code).ShouldBe(["express", "working-capital"]);
        foreach (var option in preview.Options)
        {
            option.Affordability.Level.ShouldBe(DebtLoadLevel.InsufficientData);
            option.Affordability.MonthlyProfit.ShouldBeNull();
            option.Affordability.DebtLoad.ShouldBeNull();
            option.Affordability.ProfitBasis.ShouldBeNull();
            option.Affordability.Explanation.ShouldStartWith("Нагрузку оценить нельзя");
            option.Affordability.Explanation.ShouldNotContain("10 %", customMessage: "оценка по доле выручки не выдаётся");
            option.SuggestedAmount.ShouldBeNull();
            option.TermSuggested.ShouldBeTrue();
            option.TermMonths.ShouldBe(option.Product.MaxTermMonths);
            option.Explanations.Single(e => e.Code == "term_suggested").Text.ShouldContain("потому что нагрузку оценить нельзя");
            option.Limitations.Select(l => l.Code).ShouldBe(["rate_depends_on_risk", "affordability_unknown"]);
            option.Requirements.ShouldBeEmpty("для 1 000 000 ₽ по оборотному и экспресс-кредиту выписки не требуются");
        }

        var large = _advisor.Build(company, Catalog, query with { Amount = 4_000_000, TermMonths = 12 }, Now);
        large.Options.Single(o => o.Product.Code == "working-capital").Requirements.Select(r => r.Code).ShouldBe(["financials_required"]);
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
        option.Affordability.Level.ShouldBe(DebtLoadLevel.Moderate);
        option.Affordability.Explanation.ShouldEndWith("Прибыль оценена как выручка минус расходы.");
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
        option.Affordability.MonthlyProfit.ShouldBe(-41_666.67m);
        option.Affordability.Level.ShouldBe(DebtLoadLevel.High);
        option.Affordability.Explanation.ShouldStartWith("По данным компании прибыли за последние 12 месяцев нет");
        option.TermMonths.ShouldBe(24, "без прибыли подбирается максимальный срок продукта");
        option.SuggestedAmount.ShouldBeNull("без прибыли посильная сумма не считается");
        option.Limitations.Single(l => l.Code == "high_debt_load").Text.ShouldBe("По данным компании прибыли нет: возможен отказ или снижение суммы.");
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
