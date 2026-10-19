using AlfaCredit.Core.Abstractions;

namespace AlfaCredit.UnitTests.Fakes;

/// <summary>Прозрачный "хеш" для тестов: hash(password) = "hashed:" + password.</summary>
internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";

    public bool Verify(string passwordHash, string providedPassword) => passwordHash == Hash(providedPassword);
}
