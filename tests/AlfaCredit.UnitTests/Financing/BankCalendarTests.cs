using AlfaCredit.Core.Common;
using Shouldly;

namespace AlfaCredit.UnitTests.Financing;

public sealed class BankCalendarTests
{
    [Theory]
    [InlineData("2026-10-23T20:59:59Z", "2026-10-23")] // 23:59:59 по Москве
    [InlineData("2026-10-23T21:00:00Z", "2026-10-24")] // полночь по Москве
    [InlineData("2026-10-23T14:00:00+03:00", "2026-10-23")] // московское время как есть
    [InlineData("2026-10-23T23:30:00-05:00", "2026-10-24")] // сервер в Нью-Йорке: 04:30 UTC = 07:30 по Москве следующего дня
    [InlineData("2026-12-31T21:30:00Z", "2027-01-01")] // граница года
    public void Today_is_the_calendar_date_in_moscow(string now, string expected)
    {
        BankCalendar.Today(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Moscow_offset_is_fixed_plus_three_hours()
    {
        BankCalendar.MoscowOffset.ShouldBe(TimeSpan.FromHours(3));
    }
}
