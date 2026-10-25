using AlfaCredit.Core.Domain.Checks;

namespace AlfaCredit.Infrastructure.Simulators;

/// <summary>Явные профили демо-компаний из seed. Остальные ИНН получают правдоподобные сгенерированные данные.</summary>
internal static class KnownProfiles
{
    public const string InnGood = "7701234567";
    public const string InnMedium = "503212345678";
    public const string InnReduced = "7702234567";
    public const string InnManual = "7812345678";
    public const string InnBad = "5401234567";
    public const string InnNew = "772812345678";

    public static readonly IReadOnlyDictionary<string, (bool MassAddress, bool HeadDisqualified, EgrulStatus Status)> Egrul =
        new Dictionary<string, (bool, bool, EgrulStatus)>
        {
            [InnGood] = (false, false, EgrulStatus.Active),
            [InnMedium] = (false, false, EgrulStatus.Active),
            [InnReduced] = (false, false, EgrulStatus.Active),
            [InnManual] = (false, false, EgrulStatus.Active),
            [InnBad] = (true, false, EgrulStatus.Active),
            [InnNew] = (false, false, EgrulStatus.Active),
        };

    public static readonly IReadOnlyDictionary<string, FnsResult> Fns = new Dictionary<string, FnsResult>
    {
        [InnGood] = new(0, false, 47_300_000),
        [InnMedium] = new(12_000, false, 9_100_000),
        [InnReduced] = new(12_000, false, 9_100_000),
        [InnManual] = new(0, false, 176_000_000),
        [InnBad] = new(700_000, true, 13_200_000),
        [InnNew] = new(0, false, null),
    };

    public static readonly IReadOnlyDictionary<string, CreditBureauResult> CreditBureau = new Dictionary<string, CreditBureauResult>
    {
        [InnGood] = new(60, 1, 3, 1_200_000, 150_000, 0, 0, false),
        [InnMedium] = new(30, 1, 1, 900_000, 40_000, 25, 1, false),
        [InnReduced] = new(30, 1, 1, 900_000, 100_000, 25, 1, false),
        [InnManual] = new(96, 3, 5, 40_000_000, 1_200_000, 0, 0, false),
        [InnBad] = new(48, 4, 1, 9_000_000, 600_000, 95, 3, false),
        [InnNew] = new(0, 0, 0, 0, 0, 0, 0, false),
    };

    public static readonly IReadOnlyDictionary<string, FsspResult> Fssp = new Dictionary<string, FsspResult>
    {
        [InnGood] = new(0, 0),
        [InnMedium] = new(0, 0),
        [InnReduced] = new(0, 0),
        [InnManual] = new(0, 0),
        [InnBad] = new(2, 1_400_000),
        [InnNew] = new(0, 0),
    };
}
