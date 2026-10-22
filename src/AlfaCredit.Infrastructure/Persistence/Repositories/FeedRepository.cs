using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Feed;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class FeedRepository(AppDbContext db) : IFeedRepository
{
    public void Add(FeedItem item) => db.FeedItems.Add(item);

    public async Task<PagedResult<FeedItem>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.FeedItems.AsNoTracking().Where(f => f.UserId == userId);
        if (unreadOnly)
        {
            query = query.Where(f => !f.IsRead);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(f => f.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FeedItem>(items, page, pageSize, total);
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        db.FeedItems.CountAsync(f => f.UserId == userId && !f.IsRead, cancellationToken);

    public Task<FeedItem?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        db.FeedItems.FirstOrDefaultAsync(f => f.Id == id && f.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<FeedItem>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        await db.FeedItems.Where(f => f.UserId == userId && !f.IsRead).ToListAsync(cancellationToken);
}
