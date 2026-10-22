using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.UnitTests.Fakes;

internal sealed class InMemoryUserRepository(params IEnumerable<User> users) : IUserRepository
{
    private readonly List<User> _users = [.. users];

    public Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Login == login));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> ListActiveByRoleAsync(UserRole role, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(_users.Where(u => u.Role == role && u.IsActive).ToArray());
}
