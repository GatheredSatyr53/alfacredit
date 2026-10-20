using System.Globalization;

namespace AlfaCredit.Core.Common;

/// <summary>Форматирование сумм для сообщений пользователю: «1 500 000 ₽». Не зависит от ICU и культуры процесса.</summary>
public static class Money
{
    private static readonly NumberFormatInfo Format = new()
    {
        NumberGroupSeparator = " ",
        NumberDecimalSeparator = ",",
        NumberGroupSizes = [3],
    };

    public static string FormatRub(decimal amount) => $"{amount.ToString("N0", Format)} ₽";
}
