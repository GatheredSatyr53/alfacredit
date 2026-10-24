namespace AlfaCredit.Core.Common;

/// <summary>
/// Календарь банка: «сегодня» считается по Москве (UTC+3, без перехода на летнее время с 2014 года), поэтому дата
/// не зависит от часового пояса сервера. Используется для даты, к которой нужны деньги, возраста бизнеса и ориентиров сроков.
/// </summary>
public static class BankCalendar
{
    public const string TimeZoneName = "Europe/Moscow";

    public static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    /// <summary>Календарная дата в Москве в указанный момент.</summary>
    public static DateOnly Today(DateTimeOffset now) => DateOnly.FromDateTime(now.ToOffset(MoscowOffset).DateTime);
}
