using System.Text.Json.Serialization;

namespace AlfaCredit.Core.Domain.Checks;

/// <summary>Источник внешней проверки.</summary>
public enum CheckSource
{
    /// <summary>ЕГРЮЛ / ЕГРИП: существование и статус компании.</summary>
    Egrul = 1,

    /// <summary>ФНС «Прозрачный бизнес»: налоговая задолженность, блокировки.</summary>
    Fns = 2,

    /// <summary>Бюро кредитных историй.</summary>
    CreditBureau = 3,

    /// <summary>ФССП: исполнительные производства.</summary>
    Fssp = 4,
}

public static class CheckSourceExtensions
{
    public static string Title(this CheckSource source) => source switch
    {
        CheckSource.Egrul => "ЕГРЮЛ / ЕГРИП",
        CheckSource.Fns => "ФНС «Прозрачный бизнес»",
        CheckSource.CreditBureau => "Бюро кредитных историй",
        CheckSource.Fssp => "ФССП",
        _ => source.ToString(),
    };

    /// <summary>Без этих источников решение не принимается автоматически.</summary>
    public static bool IsCritical(this CheckSource source) => source is CheckSource.Egrul or CheckSource.CreditBureau;
}

public enum EgrulStatus
{
    Active = 1,
    Liquidating = 2,
    Terminated = 3,
}

/// <summary>Сведения ЕГРЮЛ / ЕГРИП.</summary>
public sealed record EgrulResult(EgrulStatus Status, bool HeadDisqualified, bool MassAddress, DateOnly RegisteredAt, string Okved);

/// <summary>Сведения ФНС «Прозрачный бизнес».</summary>
public sealed record FnsResult(decimal TaxDebtRub, bool HasBlockedAccounts, decimal? ReportedRevenueRub);

/// <summary>Кредитная история из БКИ.</summary>
public sealed record CreditBureauResult(
    int CreditHistoryMonths,
    int ActiveLoans,
    int ClosedLoans,
    decimal TotalDebtRub,
    decimal MonthlyPaymentsRub,
    [property: JsonPropertyName("max_overdue_days_24m")] int MaxOverdueDays24M,
    [property: JsonPropertyName("overdue_count_24m")] int OverdueCount24M,
    bool HasWriteOffs);

/// <summary>Исполнительные производства ФССП.</summary>
public sealed record FsspResult(int OpenProceedings, decimal OpenAmountRub);

/// <summary>Результаты всех проверок одной заявки; отсутствующий источник равен null.</summary>
public sealed record ChecksSnapshot(EgrulResult? Egrul, FnsResult? Fns, CreditBureauResult? CreditBureau, FsspResult? Fssp)
{
    public static readonly ChecksSnapshot Empty = new(null, null, null, null);

    public IEnumerable<CheckSource> MissingSources()
    {
        if (Egrul is null)
        {
            yield return CheckSource.Egrul;
        }

        if (Fns is null)
        {
            yield return CheckSource.Fns;
        }

        if (CreditBureau is null)
        {
            yield return CheckSource.CreditBureau;
        }

        if (Fssp is null)
        {
            yield return CheckSource.Fssp;
        }
    }
}
