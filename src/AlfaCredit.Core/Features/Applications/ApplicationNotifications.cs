using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Applications;
using Microsoft.Extensions.Logging;

namespace AlfaCredit.Core.Features.Applications;

/// <summary>Уведомление подписчиков о смене статуса: сбой доставки не должен ломать сценарий.</summary>
public static class ApplicationNotifications
{
    public static async Task NotifySafeAsync(IApplicationNotifier notifier, ILogger logger, CreditApplication application, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(application);

        try
        {
            await notifier.ApplicationChangedAsync(
                new ApplicationChangedNotification(
                    application.Id,
                    application.Number,
                    application.CompanyId,
                    application.Status,
                    ApplicationStatusCatalog.Get(application.Status).Title,
                    application.UpdatedAt),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Realtime notification failed for application {Number}", application.Number);
        }
    }
}
