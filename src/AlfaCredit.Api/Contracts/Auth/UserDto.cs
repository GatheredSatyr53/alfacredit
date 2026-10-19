using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Api.Contracts.Auth;

/// <summary>Профиль пользователя.</summary>
public sealed record UserDto(Guid Id, string Login, string FullName, UserRole Role)
{
    public static UserDto From(User user) => new(user.Id, user.Login, user.FullName, user.Role);
}
