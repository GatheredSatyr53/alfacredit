using AlfaCredit.Api.Auth;
using AlfaCredit.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AlfaCredit.Api.Realtime;

/// <summary>
/// Обновления заявок в реальном времени. Клиент получает события своих компаний, сотрудник банка все.
/// Подключение: /hubs/applications с JWT в query-параметре access_token; событие applicationChanged.
/// </summary>
[Authorize]
public sealed class ApplicationsHub(ICompanyRepository companies) : Hub
{
    public const string ApplicationChangedEvent = "applicationChanged";

    public static string CompanyGroup(Guid companyId) => $"company:{companyId}";

    public const string BankGroup = "bank";

    public override async Task OnConnectedAsync()
    {
        var principal = Context.User ?? throw new HubException("Unauthenticated connection.");
        var actor = principal.ToActor();

        if (actor.IsBankEmployee)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, BankGroup, Context.ConnectionAborted);
        }
        else
        {
            foreach (var companyId in await companies.GetMemberCompanyIdsAsync(actor.UserId, Context.ConnectionAborted))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, CompanyGroup(companyId), Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }
}
