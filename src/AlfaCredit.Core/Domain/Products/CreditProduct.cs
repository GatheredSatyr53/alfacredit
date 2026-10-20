namespace AlfaCredit.Core.Domain.Products;

/// <summary>Кредитный продукт банка с границами суммы, срока и требованиями к заёмщику.</summary>
public sealed class CreditProduct
{
    public const int CodeMaxLength = 64;
    public const int NameMaxLength = 128;
    public const int DescriptionMaxLength = 1000;

    private CreditProduct()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Уникальный код в нижнем регистре, например working-capital.</summary>
    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    /// <summary>Описание «для человека»: кому и для чего подходит продукт.</summary>
    public string Description { get; private set; } = string.Empty;

    public ProductType Type { get; private set; }

    public decimal MinAmount { get; private set; }

    public decimal MaxAmount { get; private set; }

    public int MinTermMonths { get; private set; }

    public int MaxTermMonths { get; private set; }

    /// <summary>Базовая годовая ставка, %. Итоговая ставка зависит от грейда риска.</summary>
    public decimal BaseRate { get; private set; }

    public List<LoanPurpose> AllowedPurposes { get; private set; } = [];

    /// <summary>Минимальный возраст бизнеса в месяцах.</summary>
    public int MinBusinessAgeMonths { get; private set; }

    public bool RequiresCollateral { get; private set; }

    /// <summary>Сумма, до которой решение может быть принято автоматически.</summary>
    public decimal AutoApprovalLimit { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public static CreditProduct Create(
        string code,
        string name,
        string description,
        ProductType type,
        decimal minAmount,
        decimal maxAmount,
        int minTermMonths,
        int maxTermMonths,
        decimal baseRate,
        IEnumerable<LoanPurpose> allowedPurposes,
        int minBusinessAgeMonths,
        bool requiresCollateral,
        decimal autoApprovalLimit,
        int sortOrder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minAmount);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAmount, minAmount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minTermMonths);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTermMonths, minTermMonths);
        ArgumentOutOfRangeException.ThrowIfNegative(baseRate);
        ArgumentOutOfRangeException.ThrowIfNegative(minBusinessAgeMonths);
        ArgumentOutOfRangeException.ThrowIfNegative(autoApprovalLimit);

        var purposes = allowedPurposes.Distinct().ToList();
        if (purposes.Count == 0)
        {
            throw new ArgumentException("У продукта должна быть хотя бы одна допустимая цель.", nameof(allowedPurposes));
        }

        return new CreditProduct
        {
            Id = Guid.CreateVersion7(),
            Code = NormalizeCode(code),
            Name = name.Trim(),
            Description = description.Trim(),
            Type = type,
            MinAmount = minAmount,
            MaxAmount = maxAmount,
            MinTermMonths = minTermMonths,
            MaxTermMonths = maxTermMonths,
            BaseRate = baseRate,
            AllowedPurposes = purposes,
            MinBusinessAgeMonths = minBusinessAgeMonths,
            RequiresCollateral = requiresCollateral,
            AutoApprovalLimit = autoApprovalLimit,
            IsActive = true,
            SortOrder = sortOrder,
        };
    }

    public static string NormalizeCode(string code) => code.Trim().ToLowerInvariant();

    public bool AllowsPurpose(LoanPurpose purpose) => AllowedPurposes.Contains(purpose);

    public bool AllowsAmount(decimal amount) => amount >= MinAmount && amount <= MaxAmount;

    public bool AllowsTerm(int termMonths) => termMonths >= MinTermMonths && termMonths <= MaxTermMonths;
}
