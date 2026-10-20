using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Common;

/// <summary>Текущий пользователь, от имени которого выполняется сценарий.</summary>
public sealed record Actor(Guid UserId, UserRole Role)
{
    public bool IsClient => Role == UserRole.Client;

    public bool IsBankEmployee => Role is UserRole.Manager or UserRole.Admin;

    public ActorType ActorType => IsBankEmployee ? ActorType.Manager : ActorType.Client;
}
