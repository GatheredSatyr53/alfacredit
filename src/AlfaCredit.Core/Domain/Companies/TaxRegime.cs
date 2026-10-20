namespace AlfaCredit.Core.Domain.Companies;

/// <summary>Режим налогообложения.</summary>
public enum TaxRegime
{
    /// <summary>Общая система.</summary>
    Osno = 1,

    /// <summary>УСН «Доходы».</summary>
    UsnIncome = 2,

    /// <summary>УСН «Доходы минус расходы».</summary>
    UsnIncomeExpenses = 3,

    /// <summary>Патент.</summary>
    Patent = 4,
}
