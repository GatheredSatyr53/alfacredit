using AlfaCredit.Core.Common;

namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Правила для даты, к которой клиенту нужны деньги. Общие для предварительного подбора и заявки.</summary>
public static class NeedByDateRules
{
    public const string Field = "needByDate";

    /// <summary>Насколько далеко вперёд можно указать дату (два года): защищает от опечаток в годе.</summary>
    public const int MaxDaysAhead = 730;

    /// <summary>Добавляет ошибки по полю <see cref="Field"/>, если дата в прошлом или слишком далеко в будущем. Пустая дата допустима.</summary>
    public static void Validate(DateOnly? needByDate, DateOnly today, ValidationErrors errors, int maxDaysAhead = MaxDaysAhead)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (needByDate is not { } date)
        {
            return;
        }

        if (date < today)
        {
            errors.Add(Field, "Дата, к которой нужны деньги, не может быть раньше сегодняшнего дня.");
        }
        else if (date > today.AddDays(maxDaysAhead))
        {
            errors.Add(Field, $"Дата не может быть позже {today.AddDays(maxDaysAhead):dd.MM.yyyy}: планирование доступно не дальше чем на {RussianPlural.Count(maxDaysAhead, "день", "дня", "дней")} вперёд.");
        }
    }
}
