using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Contracts.Financing;

/// <summary>Три вопроса клиента для предварительного подбора: на что деньги, сколько и к какому дню. Заявка не создаётся.</summary>
public sealed class FinancingPreviewRequest
{
    /// <summary>Компания, для которой подбирается финансирование. Список своих компаний: GET /api/companies.</summary>
    /// <example>0199f3a2-5b3c-7b1a-9c0e-6d1f2a3b4c5d</example>
    [Required(ErrorMessage = "Укажите компанию.")]
    public Guid? CompanyId { get; init; }

    /// <summary>Нужная сумма в рублях.</summary>
    /// <example>1500000</example>
    [Range(1, 1_000_000_000, ErrorMessage = "Сумма должна быть положительным числом не больше 1 000 000 000 ₽.")]
    public decimal Amount { get; init; }

    /// <summary>Цель финансирования: WorkingCapital, Equipment, Expansion, Refinancing или Other.</summary>
    /// <example>WorkingCapital</example>
    [Required(ErrorMessage = "Укажите цель финансирования.")]
    public LoanPurpose? Purpose { get; init; }

    /// <summary>К какой дате нужны деньги: не раньше сегодняшнего дня и не позже чем через 2 года.</summary>
    /// <example>2026-11-15</example>
    [Required(ErrorMessage = "Укажите дату, к которой нужны деньги.")]
    public DateOnly? NeedByDate { get; init; }

    /// <summary>Желаемый срок возврата в месяцах. Если не указан, срок подбирается для каждого продукта.</summary>
    /// <example>12</example>
    [Range(1, 120, ErrorMessage = "Срок должен быть от 1 до 120 месяцев.")]
    public int? TermMonths { get; init; }
}
