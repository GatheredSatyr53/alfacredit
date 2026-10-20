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
