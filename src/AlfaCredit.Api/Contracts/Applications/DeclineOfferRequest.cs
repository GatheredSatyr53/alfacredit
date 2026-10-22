using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Отказ от предложения банка. Тело запроса необязательно.</summary>
public sealed class DeclineOfferRequest
{
    /// <summary>Причина отказа, до 1000 символов.</summary>
    /// <example>Нашли предложение с меньшей ставкой</example>
    [StringLength(1000, ErrorMessage = "Причина не длиннее 1000 символов.")]
    public string? Reason { get; init; }
}
