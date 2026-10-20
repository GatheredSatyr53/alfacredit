using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Данные новой кредитной заявки. Заявка сразу отправляется в банк.</summary>
public sealed class CreateApplicationRequest
{
    /// <summary>Идентификатор компании, от имени которой подаётся заявка. Список своих компаний: GET /api/companies.</summary>
    /// <example>0199f3a2-5b3c-7b1a-9c0e-6d1f2a3b4c5d</example>
    [Required(ErrorMessage = "Укажите компанию.")]
    public Guid? CompanyId { get; init; }

    /// <summary>Код кредитного продукта из GET /api/products.</summary>
    /// <example>working-capital</example>
    [Required(ErrorMessage = "Укажите код кредитного продукта.")]
    [StringLength(64, ErrorMessage = "Код продукта не длиннее 64 символов.")]
    public string ProductCode { get; init; } = string.Empty;

    /// <summary>Запрашиваемая сумма в рублях.</summary>
    /// <example>1500000</example>
    [Range(1, 1_000_000_000, ErrorMessage = "Сумма должна быть положительным числом не больше 1 000 000 000 ₽.")]
    public decimal Amount { get; init; }

    /// <summary>Срок в месяцах.</summary>
    /// <example>12</example>
    [Range(1, 120, ErrorMessage = "Срок должен быть от 1 до 120 месяцев.")]
    public int TermMonths { get; init; }

    /// <summary>Цель финансирования: WorkingCapital, Equipment, Expansion, Refinancing или Other.</summary>
    /// <example>WorkingCapital</example>
    [Required(ErrorMessage = "Укажите цель финансирования.")]
    public LoanPurpose? Purpose { get; init; }

    /// <summary>Свободное описание цели, до 1000 символов.</summary>
    /// <example>Закупка товара к высокому сезону</example>
    [StringLength(1000, ErrorMessage = "Описание цели не длиннее 1000 символов.")]
    public string? PurposeDescription { get; init; }
}
