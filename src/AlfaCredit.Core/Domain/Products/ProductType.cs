namespace AlfaCredit.Core.Domain.Products;

public enum ProductType
{
    /// <summary>Оборотный кредит.</summary>
    WorkingCapital = 1,

    /// <summary>Инвестиционный кредит.</summary>
    Investment = 2,

    /// <summary>Овердрафт.</summary>
    Overdraft = 3,

    /// <summary>Кредитная линия.</summary>
    CreditLine = 4,

    /// <summary>Экспресс-кредит.</summary>
    Express = 5,
}

public static class ProductTypeExtensions
{
    /// <summary>
    /// Срочный кредит с аннуитетным графиком: оборотный, экспресс и инвестиционный. По ним заранее известны сумма всех
    /// платежей и переплата. У овердрафта и кредитной линии проценты начисляются за дни фактического использования
    /// лимита, поэтому итог заранее не определён.
    /// </summary>
    public static bool HasAnnuitySchedule(this ProductType type) =>
        type is ProductType.WorkingCapital or ProductType.Express or ProductType.Investment;
}
