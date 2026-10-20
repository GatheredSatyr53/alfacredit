using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Feed;

namespace AlfaCredit.Core.Abstractions;

public interface IFeedRepository
{
    void Add(FeedItem item);

    Task<PagedResult<FeedItem>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    Task<FeedItem?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<FeedItem>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken);
}
