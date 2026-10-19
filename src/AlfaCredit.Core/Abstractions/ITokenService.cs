using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Выпущенный токен доступа.</summary>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}
