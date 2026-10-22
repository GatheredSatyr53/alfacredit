using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.Core.Features.Feed;

/// <summary>Лента событий: единственный канал уведомлений внутри продукта, email и SMS кейсом запрещены.</summary>
public sealed class FeedService(IFeedRepository feed, IUserRepository users, TimeProvider clock)
{
    /// <summary>Событие по заявке для всех представителей компании.</summary>
    public void NotifyCompany(CreditApplication application, FeedItemType type, string title, string body)
    {
        ArgumentNullException.ThrowIfNull(application);
        var now = clock.GetUtcNow();
        foreach (var member in application.Company.Members)
        {
            feed.Add(FeedItem.Create(member.UserId, application.Id, type, title, body, $"/applications/{application.Id}", now));
        }
    }

    /// <summary>Событие для всех активных менеджеров банка.</summary>
    public async Task NotifyManagersAsync(CreditApplication application, FeedItemType type, string title, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        var now = clock.GetUtcNow();
        foreach (var manager in await users.ListActiveByRoleAsync(UserRole.Manager, cancellationToken))
        {
            feed.Add(FeedItem.Create(manager.Id, application.Id, type, title, body, $"/bank/applications/{application.Id}", now));
        }
    }

    public Task<PagedResult<FeedItem>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken) =>
        feed.ListAsync(userId, unreadOnly, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        feed.CountUnreadAsync(userId, cancellationToken);

    public async Task<FeedItem> MarkReadAsync(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var item = await feed.FindAsync(id, userId, cancellationToken)
            ?? throw new NotFoundException($"Событие {id} не найдено.");
        item.MarkRead(clock.GetUtcNow());
        return item;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var items = await feed.ListUnreadAsync(userId, cancellationToken);
        foreach (var item in items)
        {
            item.MarkRead(now);
        }

        return items.Count;
    }
}
