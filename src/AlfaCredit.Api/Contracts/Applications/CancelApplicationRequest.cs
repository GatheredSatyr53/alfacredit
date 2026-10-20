using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Отмена заявки клиентом. Тело запроса необязательно.</summary>
public sealed class CancelApplicationRequest
{
    /// <summary>Причина отмены, до 1000 символов.</summary>
    /// <example>Передумали брать кредит</example>
    [StringLength(1000, ErrorMessage = "Причина не длиннее 1000 символов.")]
    public string? Reason { get; init; }
}
