namespace AlfaCredit.Core.Common;

/// <summary>Рабочие дни для ориентиров сроков: суббота и воскресенье пропускаются, праздничный календарь не учитывается.</summary>
public static class BusinessCalendar
{
    public static bool IsBusinessDay(DateOnly date) => date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    /// <summary>Дата через указанное число рабочих дней после <paramref name="from"/>; ноль дней возвращает исходную дату.</summary>
    public static DateOnly AddBusinessDays(DateOnly from, int businessDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(businessDays);

        var date = from;
        var remaining = businessDays;
        while (remaining > 0)
        {
            date = date.AddDays(1);
            if (IsBusinessDay(date))
            {
                remaining--;
            }
        }

        return date;
    }
}
