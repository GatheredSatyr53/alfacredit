namespace AlfaCredit.Core.Common;

/// <summary>Согласование числительных с существительными: 1 день, 2 дня, 5 дней.</summary>
public static class RussianPlural
{
    /// <param name="count">Число.</param>
    /// <param name="one">Форма для 1: «день».</param>
    /// <param name="few">Форма для 2–4: «дня».</param>
    /// <param name="many">Форма для 5–20 и 0: «дней».</param>
    public static string Form(long count, string one, string few, string many)
    {
        var abs = Math.Abs(count);
        var lastTwo = abs % 100;
        if (lastTwo is >= 11 and <= 19)
        {
            return many;
        }

        return (abs % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many,
        };
    }

    /// <summary>«5 дней», «1 день».</summary>
    public static string Count(long count, string one, string few, string many) => $"{count} {Form(count, one, few, many)}";
}
