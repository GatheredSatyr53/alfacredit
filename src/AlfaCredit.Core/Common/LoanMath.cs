namespace AlfaCredit.Core.Common;

/// <summary>Строка графика платежей.</summary>
/// <param name="Number">Номер платежа с 1.</param>
/// <param name="DueDate">Дата платежа.</param>
/// <param name="Payment">Сумма платежа, ₽.</param>
/// <param name="Principal">Погашение основного долга, ₽.</param>
/// <param name="Interest">Проценты за период, ₽.</param>
/// <param name="Balance">Остаток основного долга после платежа, ₽.</param>
public sealed record ScheduleRow(int Number, DateOnly DueDate, decimal Payment, decimal Principal, decimal Interest, decimal Balance);

public static class LoanMath
{
    /// <summary>Аннуитетный ежемесячный платёж по годовой ставке в процентах.</summary>
    public static decimal MonthlyPayment(decimal amount, decimal annualRatePercent, int termMonths)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termMonths);
        if (amount <= 0)
        {
            return 0;
        }

        if (annualRatePercent <= 0)
        {
            return Math.Round(amount / termMonths, 2);
        }

        var monthlyRate = (double)annualRatePercent / 100d / 12d;
        var factor = monthlyRate / (1 - Math.Pow(1 + monthlyRate, -termMonths));
        return Math.Round(amount * (decimal)factor, 2);
    }

    /// <summary>
    /// Аннуитетный график: равные платежи, проценты на остаток, последний платёж закрывает остаток ровно до нуля
    /// (поглощает копейки округления).
    /// </summary>
    public static IReadOnlyList<ScheduleRow> Schedule(decimal amount, decimal annualRatePercent, int termMonths, DateOnly firstDueDate)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termMonths);
        if (amount <= 0)
        {
            return [];
        }

        var payment = MonthlyPayment(amount, annualRatePercent, termMonths);
        var monthlyRate = annualRatePercent <= 0 ? 0m : annualRatePercent / 100m / 12m;
        var balance = amount;
        var rows = new List<ScheduleRow>(termMonths);

        for (var number = 1; number <= termMonths; number++)
        {
            var interest = Math.Round(balance * monthlyRate, 2);
            var isLast = number == termMonths;
            var principal = isLast ? balance : Math.Min(balance, payment - interest);
            var rowPayment = isLast ? principal + interest : payment;
            balance = Math.Round(balance - principal, 2);
            rows.Add(new ScheduleRow(number, firstDueDate.AddMonths(number - 1), rowPayment, principal, interest, Math.Max(0, balance)));
        }

        return rows;
    }
}
