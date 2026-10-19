using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Abstractions;

public interface IUserRepository
{
    /// <param name="login">Нормализованный логин, см. <see cref="User.NormalizeLogin"/>.</param>
    Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken);

    Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken);
}
