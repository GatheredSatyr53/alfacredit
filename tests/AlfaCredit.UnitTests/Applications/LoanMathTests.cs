using AlfaCredit.Core.Common;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class LoanMathTests
{
    [Theory]
    [InlineData(1_500_000, 19.5, 12)]
    [InlineData(2_400_000, 22.5, 24)]
    [InlineData(300_000, 0, 6)]
    [InlineData(10_000_000, 15.75, 84)]
    [InlineData(123_456.78, 21.3, 7)]
    public void Schedule_repays_exactly_the_principal(decimal amount, decimal rate, int term)
    {
        var first = new DateOnly(2026, 11, 23);

        var schedule = LoanMath.Schedule(amount, rate, term, first);

        schedule.Count.ShouldBe(term);
        schedule.Sum(r => r.Principal).ShouldBe(amount);
        schedule[^1].Balance.ShouldBe(0);
        schedule.Select(r => r.Number).ShouldBe(Enumerable.Range(1, term));
        schedule[0].DueDate.ShouldBe(first);
        schedule[^1].DueDate.ShouldBe(first.AddMonths(term - 1));
        schedule.ShouldAllBe(r => r.Payment == r.Principal + r.Interest);
        schedule.ShouldAllBe(r => r.Principal > 0 && r.Interest >= 0 && r.Balance >= 0);

        var payment = LoanMath.MonthlyPayment(amount, rate, term);
        schedule.Take(term - 1).ShouldAllBe(r => r.Payment == payment);
        Math.Abs(schedule[^1].Payment - payment).ShouldBeLessThan(5m, "последний платёж поглощает копейки округления");
    }

    [Fact]
    public void Schedule_interest_matches_the_known_annuity()
    {
        var schedule = LoanMath.Schedule(1_500_000, 19.5m, 12, new DateOnly(2026, 11, 23));

        schedule[0].Interest.ShouldBe(24_375m);
        schedule[0].Payment.ShouldBe(138_593.06m);
        schedule[0].Principal.ShouldBe(114_218.06m);
        schedule.Sum(r => r.Interest).ShouldBe(schedule.Sum(r => r.Payment) - 1_500_000);
        schedule.Sum(r => r.Interest).ShouldBeInRange(160_000m, 170_000m);
    }

    [Theory]
    [InlineData(1_500_000, 19.5, 12, 1_663_116.74, 163_116.74)]
    [InlineData(2_400_000, 22.5, 24, 3_002_423.02, 602_423.02)]
    [InlineData(300_000, 0, 6, 300_000, 0)]
    public void Totals_match_the_schedule_and_do_not_depend_on_payment_dates(decimal amount, decimal rate, int term, decimal totalPayment, decimal totalInterest)
    {
        var totals = LoanMath.Totals(amount, rate, term);

        totals.TotalPayment.ShouldBe(totalPayment);
        totals.TotalInterest.ShouldBe(totalInterest);
        totals.TotalPayment.ShouldBe(amount + totals.TotalInterest);

        foreach (var first in new[] { new DateOnly(2026, 11, 23), new DateOnly(2027, 1, 31), new DateOnly(2026, 2, 28) })
        {
            var schedule = LoanMath.Schedule(amount, rate, term, first);
            schedule.Sum(r => r.Payment).ShouldBe(totals.TotalPayment);
            schedule.Sum(r => r.Interest).ShouldBe(totals.TotalInterest);
        }
    }

    [Fact]
    public void Empty_schedule_for_zero_amount_and_guard_for_term()
    {
        LoanMath.Schedule(0, 19.5m, 12, new DateOnly(2026, 11, 23)).ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => LoanMath.Schedule(1000, 19.5m, 0, new DateOnly(2026, 11, 23)));
    }
}
