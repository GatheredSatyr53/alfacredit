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
}
