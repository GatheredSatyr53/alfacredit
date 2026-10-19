using System.Diagnostics.CodeAnalysis;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Features.Auth;

public sealed class LoginResult
{
    private LoginResult(User? user, AccessToken? token)
    {
        User = user;
        Token = token;
    }

    [MemberNotNullWhen(true, nameof(User), nameof(Token))]
    public bool IsSuccess => User is not null && Token is not null;

    public User? User { get; }

    public AccessToken? Token { get; }

    public static LoginResult Success(User user, AccessToken token) => new(user, token);

    public static LoginResult Failure() => new(null, null);
}
