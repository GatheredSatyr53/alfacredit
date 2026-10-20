using AlfaCredit.Core.Domain.Feed;

namespace AlfaCredit.Api.Contracts.Feed;

/// <summary>Событие ленты пользователя.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Type">Тип события.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Body">Текст.</param>
/// <param name="ApplicationId">Связанная заявка, если есть.</param>
/// <param name="Link">Куда вести из ленты, относительный путь кабинета.</param>
/// <param name="IsRead">Прочитано.</param>
/// <param name="CreatedAt">Когда (UTC).</param>
public sealed record FeedItemDto(
    Guid Id,
    FeedItemType Type,
    string Title,
    string Body,
    Guid? ApplicationId,
    string? Link,
    bool IsRead,
    DateTimeOffset CreatedAt)
{
    public static FeedItemDto From(FeedItem item) =>
        new(item.Id, item.Type, item.Title, item.Body, item.ApplicationId, item.Link, item.IsRead, item.CreatedAt);
}

/// <summary>Число непрочитанных событий.</summary>
public sealed record UnreadCountDto(int Unread);
