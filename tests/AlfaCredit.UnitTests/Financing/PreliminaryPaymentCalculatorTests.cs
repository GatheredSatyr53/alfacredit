using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Financing;
using AlfaCredit.Infrastructure.Persistence.Seeding;
using Shouldly;

namespace AlfaCredit.UnitTests.Financing;

public sealed class PreliminaryPaymentCalculatorTests
{
    private static readonly IReadOnlyList<CreditProduct> Products = SeedData.CreateProducts();

    private static CreditProduct Product(string code) => Products.Single(p => p.Code == code);

    [Fact]
    public void Express_loan_payment_is_annuity_by_the_base_rate_with_a_preliminary_note()
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product("express"), 1_500_000, 12);

        payment.IsAvailable.ShouldBeTrue();
        payment.Amount.ShouldBe(1_500_000);
        payment.TermMonths.ShouldBe(12);
        payment.InterestRate.ShouldBe(23m);
        payment.MonthlyPayment.ShouldBe(141_114.48m);
        payment.TotalPayment.ShouldBe(1_693_373.80m);
        payment.TotalInterest.ShouldBe(193_373.80m);
        var note = payment.Notes.ShouldHaveSingleItem();
        note.Code.ShouldBe("preliminary");
        note.Text.ShouldBe("Предварительный расчёт по базовой ставке 23 % годовых: 141 114 ₽ в месяц, переплата 193 374 ₽ за 12 месяцев. " +
                           "Итоговая ставка зависит от оценки риска: платёж в предложении банка может отличаться.");
    }

    [Fact]
    public void Payment_matches_the_preview_cost_and_the_offer_totals_for_the_same_terms()
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product("working-capital"), 1_500_000, 12);

        payment.MonthlyPayment.ShouldBe(138_593.06m, "как в preview и в предложении банка для «Ромашки»");
        payment.TotalPayment.ShouldBe(1_663_116.74m);
        payment.TotalInterest.ShouldBe(163_116.74m);
        payment.MonthlyPayment.ShouldBe(LoanMath.MonthlyPayment(1_500_000, 19.5m, 12));
        new LoanTotals(payment.TotalPayment!.Value, payment.TotalInterest!.Value).ShouldBe(LoanMath.Totals(1_500_000, 19.5m, 12));
    }

    [Theory]
    [InlineData("overdraft", 21)]
    [InlineData("credit-line", 18.5)]
    public void Limit_products_have_no_payment_because_interest_depends_on_usage(string code, decimal baseRate)
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product(code), 1_000_000, 12);

        payment.IsAvailable.ShouldBeFalse();
        payment.InterestRate.ShouldBe(baseRate);
        payment.MonthlyPayment.ShouldBeNull();
        payment.TotalPayment.ShouldBeNull();
        payment.TotalInterest.ShouldBeNull();
        var note = payment.Notes.ShouldHaveSingleItem();
        note.Code.ShouldBe("limit_product");
        note.Text.ShouldContain(Product(code).Name);
        note.Text.ShouldContain("за дни использования лимита");
    }

    [Theory]
    [InlineData(50_000, 12, "amount_below_min", "100 000 ₽")]
    [InlineData(5_000_000, 12, "amount_above_max", "3 000 000 ₽")]
    [InlineData(1_000_000, 24, "term_out_of_range", "от 3 до 18 мес.")]
    [InlineData(1_000_000, 2, "term_out_of_range", "от 3 до 18 мес.")]
    public void Amount_or_term_outside_the_product_range_gives_a_reason_with_the_preview_code(decimal amount, int term, string code, string fragment)
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product("express"), amount, term);

        payment.IsAvailable.ShouldBeFalse();
        payment.MonthlyPayment.ShouldBeNull();
        payment.Amount.ShouldBe(amount);
        payment.TermMonths.ShouldBe(term);
        var note = payment.Notes.ShouldHaveSingleItem();
        note.Code.ShouldBe(code);
        note.Text.ShouldContain(fragment);
    }

    [Fact]
    public void Both_amount_and_term_outside_the_range_are_reported_together()
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product("express"), 5_000_000, 24);

        payment.IsAvailable.ShouldBeFalse();
        payment.Notes.Select(n => n.Code).ShouldBe(["amount_above_max", "term_out_of_range"]);
    }

    [Theory]
    [InlineData(100_000, 18, 6_621.44)]
    [InlineData(3_000_000, 3, 1_038_575.91)]
    public void Product_limits_are_inclusive(decimal amount, int term, decimal expectedPayment)
    {
        var payment = PreliminaryPaymentCalculator.Calculate(Product("express"), amount, term);

        payment.IsAvailable.ShouldBeTrue();
        payment.MonthlyPayment.ShouldBe(expectedPayment);
        payment.TotalPayment.ShouldBe(amount + payment.TotalInterest!.Value);
    }

    [Fact]
    public void Only_term_loans_get_a_payment()
    {
        var available = Products.Where(p => PreliminaryPaymentCalculator.Calculate(p, 1_000_000, 12).IsAvailable).Select(p => p.Code);

        available.ShouldBe(["working-capital", "express", "investment"], ignoreOrder: true);
    }

    [Fact]
    public void Guards_reject_non_positive_amount_and_term()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PreliminaryPaymentCalculator.Calculate(Product("express"), 0, 12));
        Should.Throw<ArgumentOutOfRangeException>(() => PreliminaryPaymentCalculator.Calculate(Product("express"), 1_000_000, 0));
    }
}
