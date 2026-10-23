using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Scoring;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Decisions;

/// <summary>Демо-режим: ответ менеджера по заявкам в ручном рассмотрении моделируется системой, пока нет очереди менеджера.</summary>
public sealed class DemoManagerOptions
{
    public const string SectionName = "DemoManager";

    /// <summary>Включает заглушку. Выключить, когда появятся настоящие действия менеджера.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Пауза между передачей менеджеру и ответом: статус «Рассмотрение менеджером» успевает показаться в кабинете.</summary>
    [Range(0, 600)]
    public int DelaySeconds { get; set; } = 8;

    /// <summary>Вероятность одобрения; 0.5 означает 50/50.</summary>
    [Range(0, 1)]
    public double ApprovalProbability { get; set; } = 0.5;

    public TimeSpan Delay => TimeSpan.FromSeconds(DelaySeconds);
}

/// <summary>Выбор исхода демо-менеджера; в тестах подменяется предсказуемым.</summary>
public interface IDemoManagerChooser
{
    bool Approves();
}

public sealed class RandomDemoManagerChooser(IOptions<DemoManagerOptions> options) : IDemoManagerChooser
{
    public bool Approves() => Random.Shared.NextDouble() < options.Value.ApprovalProbability;
}

/// <summary>
/// Правила смоделированного менеджера: простые и объяснимые. Решение помечено как смоделированное в самой записи,
/// в причинах (код demo_manager) и в истории заявки, чтобы на демо это не выглядело настоящим решением сотрудника.
/// </summary>
public static class DemoManagerPolicy
{
    public const string SimulatedReasonCode = "demo_manager";
    public const string ApprovedReasonCode = "manual_review_approved";
    public const string RejectedReasonCode = "manual_review_rejected";

    /// <summary>Менеджер не одобряет больше половины годовой выручки.</summary>
    public const decimal MaxAmountShareOfRevenue = 0.5m;

    public static readonly DecisionReason SimulatedReason = new(
        SimulatedReasonCode,
        "Ответ менеджера смоделирован (демо-режим)",
        "В демонстрационном стенде решение менеджера выбирается автоматически, настоящий сотрудник заявку не рассматривал.");

    /// <summary>Одобрение: запрошенная сумма не выше половины выручки, ставка с надбавкой по грейду, при слабой оценке дополнительное условие.</summary>
    public static Decision Approve(CreditApplication application, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        var product = application.Product;
        var company = application.Company;
        var scoring = application.LatestScoring;
        var grade = scoring?.Grade ?? RiskGrade.C;

        var rate = product.BaseRate + RiskGrades.RatePremium(grade);
        var amount = application.Amount;
        var conditions = new List<string>();
        var reasons = new List<DecisionReason>
        {
            new(ApprovedReasonCode, "Одобрено по результатам ручного рассмотрения", "Менеджер изучил заявку и подтвердил возможность финансирования."),
        };

        var cap = Math.Max(product.MinAmount, Math.Round(company.AnnualRevenue * MaxAmountShareOfRevenue, 0));
        if (amount > cap)
        {
            amount = cap;
            conditions.Add($"сумма снижена до {Money.FormatRub(cap)} ({MaxAmountShareOfRevenue:P0} годовой выручки)");
            reasons.Add(new DecisionReason("amount_reduced", "Сумма снижена", $"Менеджер одобрил {Money.FormatRub(cap)}: это половина годовой выручки компании, комфортный уровень нагрузки."));
        }

        if (rate > product.BaseRate)
        {
            conditions.Add($"ставка {rate:0.##} % годовых с учётом надбавки за риск (грейд {grade})");
        }

        if (scoring is null || grade == RiskGrade.D)
        {
            conditions.Add("до выдачи предоставить выписку по расчётному счёту за последние 6 месяцев");
        }

        reasons.Add(SimulatedReason);

        var type = conditions.Count == 0 ? DecisionType.Approved : DecisionType.ApprovedWithConditions;
        return Decision.Create(
            application.Id,
            type,
            DecisionActor.Manager,
            null,
            null,
            amount,
            application.TermMonths,
            rate,
            LoanMath.MonthlyPayment(amount, rate, application.TermMonths),
            conditions.Count == 0 ? null : string.Join("; ", conditions),
            reasons,
            now,
            isSimulated: true);
    }

    /// <summary>Отказ: окончательный, с объяснением и причинами, из-за которых автоматика передала заявку менеджеру.</summary>
    public static Decision Reject(CreditApplication application, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(application);
        var reasons = new List<DecisionReason>
        {
            new(RejectedReasonCode, "Отказ по результатам ручного рассмотрения", "Менеджер изучил заявку и не может одобрить финансирование: совокупный риск для банка слишком высок. Вы можете подать новую заявку на меньшую сумму или через три месяца."),
        };

        var handoff = application.Decisions.Where(d => d.Type == DecisionType.ManualReview).OrderByDescending(d => d.DecidedAt).FirstOrDefault();
        if (handoff is not null)
        {
            reasons.AddRange(handoff.Reasons.Take(2));
        }

        reasons.Add(SimulatedReason);
        return Decision.Create(application.Id, DecisionType.Rejected, DecisionActor.Manager, null, null, null, null, null, null, null, reasons, now, isSimulated: true);
    }
}
