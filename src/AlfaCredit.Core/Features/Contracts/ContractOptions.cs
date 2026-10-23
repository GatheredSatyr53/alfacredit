using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Core.Features.Contracts;

/// <summary>Подписание договора и выдача.</summary>
public sealed class ContractOptions
{
    public const string SectionName = "Contracts";

    /// <summary>Сколько дней договор ожидает подписи; затем заявка отменяется системой.</summary>
    [Range(1, 90)]
    public int SigningDays { get; set; } = 5;

    /// <summary>Срок действия кода подтверждения.</summary>
    [Range(1, 1440)]
    public int CodeTtlMinutes { get; set; } = 10;

    /// <summary>Попыток ввода одного кода.</summary>
    [Range(1, 20)]
    public int CodeMaxAttempts { get; set; } = 5;

    /// <summary>Возвращать код в ответе на запрос кода (демо-режим). В ленту кабинета код приходит всегда.</summary>
    public bool RevealCodeInResponse { get; set; } = true;

    /// <summary>Наименование кредитора в договоре.</summary>
    [Required]
    [StringLength(200)]
    public string LenderName { get; set; } = "AlfaCredit (демонстрационный банк)";

    public TimeSpan SigningPeriod => TimeSpan.FromDays(SigningDays);

    public TimeSpan CodeTtl => TimeSpan.FromMinutes(CodeTtlMinutes);
}
