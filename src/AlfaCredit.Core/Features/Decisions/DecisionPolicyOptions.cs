using System.ComponentModel.DataAnnotations;

namespace AlfaCredit.Core.Features.Decisions;

/// <summary>Параметры кредитной политики. Описаны в docs/decision-logic.md.</summary>
public sealed class DecisionPolicyOptions
{
    public const string SectionName = "DecisionPolicy";

    [Range(1, 90)]
    public int OfferValidityDays { get; set; } = 7;

    /// <summary>Доля годовой выручки, до которой снижается сумма при одобрении с условиями.</summary>
    [Range(0.01, 1)]
    public decimal MaxAmountShareOfRevenue { get; set; } = 0.25m;

    [Range(0, 100_000_000)]
    public decimal TaxDebtHardStopRub { get; set; } = 500_000;

    [Range(0, 1_000_000_000)]
    public decimal FsspHardStopRub { get; set; } = 1_000_000;

    [Range(1, 3650)]
    public int OverdueDaysHardStop { get; set; } = 90;

    public TimeSpan OfferValidity => TimeSpan.FromDays(OfferValidityDays);
}
