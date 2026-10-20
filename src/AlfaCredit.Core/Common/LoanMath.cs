namespace AlfaCredit.Core.Common;

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
}
