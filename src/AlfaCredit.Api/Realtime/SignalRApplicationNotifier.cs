using AlfaCredit.Core.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace AlfaCredit.Api.Realtime;

internal sealed class SignalRApplicationNotifier(IHubContext<ApplicationsHub> hub) : IApplicationNotifier
{
    public async Task ApplicationChangedAsync(ApplicationChangedNotification notification, CancellationToken cancellationToken)
    {
        var payload = new
        {
            applicationId = notification.ApplicationId,
            number = notification.Number,
            companyId = notification.CompanyId,
            status = notification.Status.ToString(),
            statusTitle = notification.StatusTitle,
            updatedAt = notification.UpdatedAt,
        };

        await hub.Clients
            .Groups(ApplicationsHub.CompanyGroup(notification.CompanyId), ApplicationsHub.BankGroup)
            .SendAsync(ApplicationsHub.ApplicationChangedEvent, payload, cancellationToken);
    }
}
