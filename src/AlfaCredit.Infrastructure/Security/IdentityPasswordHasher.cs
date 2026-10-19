using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace AlfaCredit.Infrastructure.Security;

/// <summary>PBKDF2 из ASP.NET Core Identity: соль и число итераций хранятся внутри хеша.</summary>
internal sealed class IdentityPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password) => _hasher.HashPassword(null!, password);

    public bool Verify(string passwordHash, string providedPassword) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, providedPassword) != PasswordVerificationResult.Failed;
}
