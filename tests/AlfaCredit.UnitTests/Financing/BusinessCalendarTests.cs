using AlfaCredit.Core.Common;
using Shouldly;

namespace AlfaCredit.UnitTests.Financing;

public sealed class BusinessCalendarTests
{
    [Theory]
    [InlineData("2026-10-23", 0, "2026-10-23")] // пятница, ноль дней
    [InlineData("2026-10-23", 1, "2026-10-26")] // пятница + 1 = понедельник
    [InlineData("2026-10-24", 1, "2026-10-26")] // суббота + 1 = понедельник
    [InlineData("2026-10-23", 3, "2026-10-28")] // пятница + 3 = среда
    [InlineData("2026-10-23", 10, "2026-11-06")] // две рабочие недели
    [InlineData("2026-10-21", 2, "2026-10-23")] // среда + 2 = пятница
    public void Adds_business_days_skipping_weekends(string from, int days, string expected)
    {
        BusinessCalendar.AddBusinessDays(DateOnly.Parse(from), days).ShouldBe(DateOnly.Parse(expected));
    }

    [Fact]
    public void Negative_days_are_rejected()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => BusinessCalendar.AddBusinessDays(new DateOnly(2026, 10, 23), -1));
    }

    [Theory]
    [InlineData(1, "день")]
    [InlineData(2, "дня")]
    [InlineData(5, "дней")]
    [InlineData(11, "дней")]
    [InlineData(21, "день")]
    [InlineData(22, "дня")]
    [InlineData(111, "дней")]
    public void Russian_plural_forms(int count, string expected)
    {
        RussianPlural.Form(count, "день", "дня", "дней").ShouldBe(expected);
    }
}
