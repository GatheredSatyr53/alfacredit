using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using AlfaCredit.Core.Common;

namespace AlfaCredit.Api.Mapping;

/// <summary>
/// Клиентское представление известных технических формулировок, в том числе сохранённых до изменения API.
/// Исходные записи и представление сотрудника банка остаются без изменений.
/// </summary>
public static partial class ClientDecisionText
{
    [return: NotNullIfNotNull(nameof(text))]
    public static string? ForActor(string? text, Actor actor)
    {
        if (text is null || actor.IsBankEmployee)
        {
            return text;
        }

        return GradeInConditions().Replace(ScoreHeading().Replace(text, "Результат оценки"), string.Empty);
    }

    [GeneratedRegex(@"\bГрейд\s+риска\s+[A-E],\s*[0-9]{1,3}\s+баллов\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScoreHeading();

    [GeneratedRegex(@"\s*\(грейд\s+[A-E]\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GradeInConditions();
}
