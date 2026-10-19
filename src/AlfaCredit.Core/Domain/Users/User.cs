namespace AlfaCredit.Core.Domain.Users;

/// <summary>Учётная запись пользователя системы.</summary>
public sealed class User
{
    public const int LoginMaxLength = 64;
    public const int FullNameMaxLength = 256;
    public const int PasswordHashMaxLength = 512;

    private User()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>Логин в нижнем регистре, уникален.</summary>
    public string Login { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string FullName { get; private set; } = string.Empty;

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static User Create(string login, string passwordHash, string fullName, UserRole role, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullName);

        return new User
        {
            Id = Guid.CreateVersion7(),
            Login = NormalizeLogin(login),
            PasswordHash = passwordHash,
            FullName = fullName.Trim(),
            Role = role,
            IsActive = true,
            CreatedAt = createdAt,
        };
    }

    public static string NormalizeLogin(string login) => login.Trim().ToLowerInvariant();

    public void ChangePasswordHash(string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        PasswordHash = passwordHash;
    }

    public void Deactivate() => IsActive = false;
}
