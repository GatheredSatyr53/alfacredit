using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Событие для подписчиков в реальном времени: кабинет клиента и очередь банка.</summary>
public sealed record ApplicationChangedNotification(
    Guid ApplicationId,
    string Number,
    Guid CompanyId,
    ApplicationStatus Status,
    string StatusTitle,
    DateTimeOffset UpdatedAt);

public interface IApplicationNotifier
{
    Task ApplicationChangedAsync(ApplicationChangedNotification notification, CancellationToken cancellationToken);
}
