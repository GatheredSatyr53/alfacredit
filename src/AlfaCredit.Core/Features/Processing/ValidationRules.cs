using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Processing;

/// <summary>Правила полноты данных этапа Validate. Вынесены отдельно, чтобы предварительный подбор предупреждал о них заранее.</summary>
public static class ValidationRules
{
    /// <summary>Сумма, начиная с которой банку нужны расходы и чистая прибыль за 12 месяцев.</summary>
    public const decimal StatementsAmountThreshold = 3_000_000;

    /// <summary>Нужны ли расходы и чистая прибыль: крупные суммы и продукты «Кредитная линия», «Инвестиционный кредит».</summary>
    public static bool NeedsFinancialStatements(decimal amount, ProductType productType) =>
        amount > StatementsAmountThreshold || productType is ProductType.Investment or ProductType.CreditLine;
}
