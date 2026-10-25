using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Common;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Contracts.Products;

/// <summary>
/// Сумма и срок для предварительного ежемесячного платежа по продуктам каталога. Указываются вместе; без них
/// каталог отдаёт продукты без расчёта.
/// </summary>
public sealed class PreliminaryPaymentQuery
{
    /// <summary>Сумма кредита в рублях.</summary>
    /// <example>1500000</example>
    [FromQuery(Name = "amount")]
    [Range(1, 1_000_000_000, ErrorMessage = "Сумма должна быть положительным числом не больше 1 000 000 000 ₽.")]
    public decimal? Amount { get; init; }

    /// <summary>Срок в месяцах.</summary>
    /// <example>12</example>
    [FromQuery(Name = "termMonths")]
    [Range(1, 120, ErrorMessage = "Срок должен быть от 1 до 120 месяцев.")]
    public int? TermMonths { get; init; }

    /// <summary>Сумма и срок, если запрошен расчёт; null, если оба параметра не переданы. Один параметр без другого: 400.</summary>
    public (decimal Amount, int TermMonths)? RequestedTerms()
    {
        if (Amount is null && TermMonths is null)
        {
            return null;
        }

        var errors = new ValidationErrors();
        if (Amount is null)
        {
            errors.Add("amount", "Для расчёта платежа укажите сумму вместе со сроком.");
        }

        if (TermMonths is null)
        {
            errors.Add("termMonths", "Для расчёта платежа укажите срок вместе с суммой.");
        }

        errors.ThrowIfAny();
        return (Amount!.Value, TermMonths!.Value);
    }
}
