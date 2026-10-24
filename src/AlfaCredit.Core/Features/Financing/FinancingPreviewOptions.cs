using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>Параметры предварительного подбора: ориентиры сроков получения денег и пороги кредитной нагрузки. Описаны в docs/financing-preview.md.</summary>
public sealed class FinancingPreviewOptions
{
    public const string SectionName = "FinancingPreview";

    /// <summary>Рабочих дней до денег на счёте при автоматическом решении.</summary>
    [Range(0, 60)]
    public int AutoDecisionBusinessDays { get; set; } = 1;

    /// <summary>Рабочих дней до денег, если решение принимает менеджер.</summary>
    [Range(0, 60)]
    public int ManualReviewBusinessDays { get; set; } = 3;

    /// <summary>Рабочих дней до денег по продуктам с залогом: оформление и оценка залога.</summary>
    [Range(0, 120)]
    public int CollateralBusinessDays { get; set; } = 10;

    /// <summary>Доля месячной прибыли, до которой платежи по кредитам считаются комфортными.</summary>
    [Range(0.01, 1)]
    public decimal ComfortableDebtLoad { get; set; } = 0.30m;

    /// <summary>Доля месячной прибыли, с которой нагрузка считается повышенной: в правилах скоринга rules-0.3 начинается штраф.</summary>
    [Range(0.01, 10)]
    public decimal ElevatedDebtLoad { get; set; } = 0.50m;

    /// <summary>Доля месячной прибыли, с которой нагрузка считается высокой: в правилах скоринга rules-0.3 максимальный штраф.</summary>
    [Range(0.01, 10)]
    public decimal HighDebtLoad { get; set; } = 0.70m;

    /// <summary>Насколько далеко вперёд можно указать дату, к которой нужны деньги.</summary>
    [Range(1, 3650)]
    public int MaxNeedByDays { get; set; } = NeedByDateRules.MaxDaysAhead;
}
