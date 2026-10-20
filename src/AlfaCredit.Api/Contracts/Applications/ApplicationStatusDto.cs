using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Статус заявки с текстами для интерфейса.</summary>
/// <param name="Code">Код статуса.</param>
/// <param name="Title">Короткое название.</param>
/// <param name="Description">Что происходит, простым языком для клиента.</param>
/// <param name="ClientNextAction">Что сейчас требуется от клиента.</param>
/// <param name="BankNextAction">Что сейчас требуется от банка.</param>
/// <param name="IsTerminal">Конечный статус.</param>
/// <param name="ProgressStep">Шаг прогресс-бара, 0..TotalSteps.</param>
/// <param name="TotalSteps">Всего шагов прогресс-бара.</param>
public sealed record ApplicationStatusDto(
    ApplicationStatus Code,
    string Title,
    string Description,
    string ClientNextAction,
    string BankNextAction,
    bool IsTerminal,
    int ProgressStep,
    int TotalSteps)
{
    public static ApplicationStatusDto From(ApplicationStatusInfo info) => new(
        info.Code,
        info.Title,
        info.ClientDescription,
        info.ClientNextAction,
        info.BankNextAction,
        info.IsTerminal,
        info.ProgressStep,
        ApplicationStatusCatalog.TotalProgressSteps);

    public static ApplicationStatusDto From(ApplicationStatus status) => From(ApplicationStatusCatalog.Get(status));
}
