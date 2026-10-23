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

    /// <summary>Ставка для текстов: «19,5 %».</summary>
    public static string FormatRate(decimal annualRatePercent) => $"{annualRatePercent.ToString("0.##", Format)} %";

    /// <summary>Округление до целых рублей для внешних систем, которые ждут целые числа (контракт скоринга).</summary>
    public static decimal WholeRubles(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static decimal? WholeRubles(decimal? amount) => amount is { } value ? WholeRubles(value) : null;
}
