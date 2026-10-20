namespace AlfaCredit.Core.Domain.Products;

/// <summary>Цель финансирования, которую выбирает предприниматель.</summary>
public enum LoanPurpose
{
    /// <summary>Пополнение оборотных средств.</summary>
    WorkingCapital = 1,

    /// <summary>Покупка оборудования или транспорта.</summary>
    Equipment = 2,

    /// <summary>Расширение бизнеса, новое направление.</summary>
    Expansion = 3,

    /// <summary>Рефинансирование текущих кредитов.</summary>
    Refinancing = 4,

    /// <summary>Другая цель.</summary>
    Other = 5,
}

public static class LoanPurposeExtensions
{
    public static string Title(this LoanPurpose purpose) => purpose switch
    {
        LoanPurpose.WorkingCapital => "Пополнение оборотных средств",
        LoanPurpose.Equipment => "Покупка оборудования или транспорта",
        LoanPurpose.Expansion => "Расширение бизнеса",
        LoanPurpose.Refinancing => "Рефинансирование кредитов",
        LoanPurpose.Other => "Другая цель",
        _ => purpose.ToString(),
    };
}
