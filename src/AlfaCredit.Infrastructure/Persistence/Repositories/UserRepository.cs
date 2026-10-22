using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Login == login, cancellationToken);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task<IReadOnlyList<User>> ListActiveByRoleAsync(UserRole role, CancellationToken cancellationToken) =>
        await db.Users.AsNoTracking().Where(u => u.Role == role && u.IsActive).OrderBy(u => u.Login).ToListAsync(cancellationToken);
}
