using AlfaCredit.Core.Domain.Users;
using Shouldly;

namespace AlfaCredit.UnitTests.Auth;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 19, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_fields_normalizes_login_and_activates_user()
    {
        var user = User.Create("  Client_Good ", "hash", "  Мария Иванова ", UserRole.Client, Now);

        user.Id.ShouldNotBe(Guid.Empty);
        user.Login.ShouldBe("client_good");
        user.FullName.ShouldBe("Мария Иванова");
        user.Role.ShouldBe(UserRole.Client);
        user.IsActive.ShouldBeTrue();
        user.CreatedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData("", "hash", "Имя")]
    [InlineData("login", "", "Имя")]
    [InlineData("login", "hash", " ")]
    public void Create_rejects_blank_required_fields(string login, string hash, string fullName)
    {
        Should.Throw<ArgumentException>(() => User.Create(login, hash, fullName, UserRole.Client, Now));
    }
}
