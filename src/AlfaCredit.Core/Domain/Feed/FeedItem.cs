namespace AlfaCredit.Core.Domain.Feed;

/// <summary>Тип события в ленте клиента или сотрудника.</summary>
public enum FeedItemType
{
    StatusChanged = 1,
    ClarificationRequested = 2,
    OfferReady = 3,
    Rejected = 4,
    ManualReview = 5,
    ContractReady = 6,
    Disbursed = 7,
    Recommendation = 8,
    News = 9,

    /// <summary>Код подтверждения для подписания договора (имитация SMS).</summary>
    SigningCode = 10,

    ContractSigned = 11,
}

/// <summary>Событие в персональной ленте пользователя: вместо email и SMS, которые запрещены кейсом.</summary>
public sealed class FeedItem
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 2000;
    public const int LinkMaxLength = 200;

    private FeedItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public FeedItemType Type { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    /// <summary>Относительный путь в кабинете, например /applications/{id}.</summary>
    public string? Link { get; private set; }

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public static FeedItem Create(Guid userId, Guid? applicationId, FeedItemType type, string title, string body, string? link, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        return new FeedItem
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            ApplicationId = applicationId,
            Type = type,
            Title = title.Length > TitleMaxLength ? title[..TitleMaxLength] : title,
            Body = body.Length > BodyMaxLength ? body[..BodyMaxLength] : body,
            Link = link is { Length: > LinkMaxLength } ? link[..LinkMaxLength] : link,
            IsRead = false,
            CreatedAt = now,
        };
    }

    public void MarkRead(DateTimeOffset now)
    {
        if (IsRead)
        {
            return;
        }

        IsRead = true;
        ReadAt = now;
    }
}
