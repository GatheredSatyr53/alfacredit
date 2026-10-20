using AlfaCredit.Core.Common;

namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Единственное место, где описаны допустимые переходы статусов заявки и кто вправе их выполнять.</summary>
public static class ApplicationWorkflow
{
    private static readonly IReadOnlyDictionary<ApplicationStatus, IReadOnlyList<Transition>> Transitions =
        new Dictionary<ApplicationStatus, IReadOnlyList<Transition>>
        {
            [ApplicationStatus.Draft] =
            [
                new(ApplicationStatus.Submitted, ActorType.Client),
                new(ApplicationStatus.Cancelled, ActorType.Client),
            ],
            [ApplicationStatus.Submitted] =
            [
                new(ApplicationStatus.Validating, ActorType.System),
                new(ApplicationStatus.ManualReview, ActorType.System),
                new(ApplicationStatus.Cancelled, ActorType.Client),
            ],
            [ApplicationStatus.Validating] =
            [
                new(ApplicationStatus.Enriching, ActorType.System),
                new(ApplicationStatus.NeedsClarification, ActorType.System, ActorType.Manager),
                new(ApplicationStatus.ManualReview, ActorType.System),
                new(ApplicationStatus.Cancelled, ActorType.Client),
            ],
            [ApplicationStatus.NeedsClarification] =
            [
                new(ApplicationStatus.Validating, ActorType.Client),
                new(ApplicationStatus.Cancelled, ActorType.Client),
            ],
            [ApplicationStatus.Enriching] =
            [
                new(ApplicationStatus.Scoring, ActorType.System),
                new(ApplicationStatus.ManualReview, ActorType.System),
            ],
            [ApplicationStatus.Scoring] =
            [
                new(ApplicationStatus.Deciding, ActorType.System),
                new(ApplicationStatus.ManualReview, ActorType.System),
            ],
            [ApplicationStatus.Deciding] =
            [
                new(ApplicationStatus.Approved, ActorType.System),
                new(ApplicationStatus.ApprovedWithConditions, ActorType.System),
                new(ApplicationStatus.ManualReview, ActorType.System),
                new(ApplicationStatus.Rejected, ActorType.System),
            ],
            [ApplicationStatus.ManualReview] =
            [
                new(ApplicationStatus.NeedsClarification, ActorType.Manager),
                new(ApplicationStatus.Approved, ActorType.Manager),
                new(ApplicationStatus.ApprovedWithConditions, ActorType.Manager),
                new(ApplicationStatus.Rejected, ActorType.Manager),
                new(ApplicationStatus.Cancelled, ActorType.Client),
            ],
            [ApplicationStatus.Approved] =
            [
                new(ApplicationStatus.ContractSigning, ActorType.Client),
                new(ApplicationStatus.OfferDeclined, ActorType.Client, ActorType.System),
            ],
            [ApplicationStatus.ApprovedWithConditions] =
            [
                new(ApplicationStatus.ContractSigning, ActorType.Client),
                new(ApplicationStatus.OfferDeclined, ActorType.Client, ActorType.System),
            ],
            [ApplicationStatus.ContractSigning] =
            [
                new(ApplicationStatus.Signed, ActorType.Client),
            ],
            [ApplicationStatus.Signed] =
            [
                new(ApplicationStatus.Disbursed, ActorType.System),
            ],
        };

    public static bool CanTransition(ApplicationStatus from, ApplicationStatus to, ActorType actor) =>
        Transitions.TryGetValue(from, out var transitions)
        && transitions.Any(t => t.To == to && t.Actors.Contains(actor));

    /// <summary>Статусы, в которые указанный участник может перевести заявку из текущего.</summary>
    public static IReadOnlyList<ApplicationStatus> NextStatuses(ApplicationStatus from, ActorType actor) =>
        Transitions.TryGetValue(from, out var transitions)
            ? transitions.Where(t => t.Actors.Contains(actor)).Select(t => t.To).ToArray()
            : [];

    public static void EnsureAllowed(ApplicationStatus from, ApplicationStatus to, ActorType actor)
    {
        if (!CanTransition(from, to, actor))
        {
            throw new InvalidStatusTransitionException(from, to, actor);
        }
    }

    private sealed record Transition(ApplicationStatus To, params ActorType[] Actors);
}

public sealed class InvalidStatusTransitionException(ApplicationStatus from, ApplicationStatus to, ActorType actor)
    : DomainException(
        $"Действие недоступно: заявка в статусе «{ApplicationStatusCatalog.Get(from).Title}» не может перейти в статус «{ApplicationStatusCatalog.Get(to).Title}».")
{
    public ApplicationStatus From { get; } = from;

    public ApplicationStatus To { get; } = to;

    public ActorType Actor { get; } = actor;
}
