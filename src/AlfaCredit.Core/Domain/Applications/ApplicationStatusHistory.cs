using AlfaCredit.Core.Common;

namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Запись о смене статуса: таймлайн заявки для клиента и аудит для банка.</summary>
public sealed class ApplicationStatusHistory
{
    public const int CommentMaxLength = 1000;

    private ApplicationStatusHistory()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    /// <summary>Предыдущий статус; пусто для записи о создании.</summary>
    public ApplicationStatus? FromStatus { get; private set; }

    public ApplicationStatus ToStatus { get; private set; }

    public ActorType ActorType { get; private set; }

    /// <summary>Пользователь, выполнивший действие; пусто для системы.</summary>
    public Guid? ActorUserId { get; private set; }

    public string? Comment { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    internal static ApplicationStatusHistory Create(
        Guid applicationId,
        ApplicationStatus? fromStatus,
        ApplicationStatus toStatus,
        ActorType actorType,
        Guid? actorUserId,
        string? comment,
        DateTimeOffset occurredAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorType = actorType,
            ActorUserId = actorUserId,
            Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim(),
            OccurredAt = occurredAt,
        };
}
