using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Features.Auth;

/// <summary>Вход по логину и паролю. Не раскрывает, что именно неверно: логин или пароль.</summary>
public sealed class AuthService(IUserRepository users, IPasswordHasher passwordHasher, ITokenService tokenService)
{
    public async Task<LoginResult> LoginAsync(string login, string password, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
        {
            return LoginResult.Failure();
        }

        var user = await users.FindByLoginAsync(User.NormalizeLogin(login), cancellationToken);
        if (user is null || !user.IsActive)
        {
            return LoginResult.Failure();
        }

        if (!passwordHasher.Verify(user.PasswordHash, password))
        {
            return LoginResult.Failure();
        }

        return LoginResult.Success(user, tokenService.CreateAccessToken(user));
    }
}
