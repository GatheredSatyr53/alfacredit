using AlfaCredit.Core.Domain.Users;
using AlfaCredit.Core.Features.Auth;
using AlfaCredit.UnitTests.Fakes;
using Shouldly;

namespace AlfaCredit.UnitTests.Auth;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    private readonly FakePasswordHasher _hasher = new();

    [Fact]
    public async Task Login_with_valid_credentials_returns_user_and_token()
    {
        var user = User.Create("client_good", _hasher.Hash("Demo123!"), "Мария Иванова", UserRole.Client, Now);
        var service = CreateService(user);

        var result = await service.LoginAsync("client_good", "Demo123!", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.User.ShouldBeSameAs(user);
        result.Token.Value.ShouldBe("token-for:client_good");
        result.Token.ExpiresAt.ShouldBe(FakeTokenService.ExpiresAt);
    }

    [Fact]
    public async Task Login_normalizes_login_case_and_whitespace()
    {
        var user = User.Create("Manager", _hasher.Hash("Demo123!"), "Алексей Петров", UserRole.Manager, Now);
        var service = CreateService(user);

        var result = await service.LoginAsync("  MANAGER ", "Demo123!", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        user.Login.ShouldBe("manager");
    }

    [Fact]
    public async Task Login_with_wrong_password_fails()
    {
        var user = User.Create("client_good", _hasher.Hash("Demo123!"), "Мария Иванова", UserRole.Client, Now);
        var service = CreateService(user);

        var result = await service.LoginAsync("client_good", "wrong", CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
        result.User.ShouldBeNull();
        result.Token.ShouldBeNull();
    }

    [Fact]
    public async Task Login_with_unknown_login_fails()
    {
        var service = CreateService();

        var result = await service.LoginAsync("nobody", "Demo123!", CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task Login_for_deactivated_user_fails_even_with_correct_password()
    {
        var user = User.Create("client_bad", _hasher.Hash("Demo123!"), "Дмитрий Волков", UserRole.Client, Now);
        user.Deactivate();
        var service = CreateService(user);

        var result = await service.LoginAsync("client_bad", "Demo123!", CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    [Theory]
    [InlineData("", "Demo123!")]
    [InlineData("   ", "Demo123!")]
    [InlineData("client_good", "")]
    public async Task Login_with_empty_credentials_fails_without_lookup(string login, string password)
    {
        var service = CreateService();

        var result = await service.LoginAsync(login, password, CancellationToken.None);

        result.IsSuccess.ShouldBeFalse();
    }

    private AuthService CreateService(params User[] users) =>
        new(new InMemoryUserRepository(users), _hasher, new FakeTokenService());
}
