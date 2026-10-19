using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.UnitTests.Fakes;

internal sealed class FakeTokenService : ITokenService
{
    public static readonly DateTimeOffset ExpiresAt = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public AccessToken CreateAccessToken(User user) => new($"token-for:{user.Login}", ExpiresAt);
}
