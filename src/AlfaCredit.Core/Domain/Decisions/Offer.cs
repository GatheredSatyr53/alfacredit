namespace AlfaCredit.Core.Domain.Decisions;

public enum OfferResponse
{
    Pending = 1,
    Accepted = 2,
    Declined = 3,
    Expired = 4,
}

/// <summary>Предложение банка по одобренной заявке. Действует ограниченное время.</summary>
public sealed class Offer
{
    private Offer()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid DecisionId { get; private set; }

    public decimal Amount { get; private set; }

    public int TermMonths { get; private set; }

    public decimal InterestRate { get; private set; }

    public decimal MonthlyPayment { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public OfferResponse Response { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsPending => Response == OfferResponse.Pending;

    public bool IsExpired(DateTimeOffset now) => IsPending && now >= ExpiresAt;

    public static Offer Create(Guid applicationId, Guid decisionId, decimal amount, int termMonths, decimal interestRate, decimal monthlyPayment, DateTimeOffset now, TimeSpan validity) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            DecisionId = decisionId,
            Amount = amount,
            TermMonths = termMonths,
            InterestRate = interestRate,
            MonthlyPayment = monthlyPayment,
            ExpiresAt = now + validity,
            Response = OfferResponse.Pending,
            CreatedAt = now,
        };

    internal void Accept(DateTimeOffset now)
    {
        Response = OfferResponse.Accepted;
        RespondedAt = now;
    }

    internal void Decline(DateTimeOffset now)
    {
        Response = OfferResponse.Declined;
        RespondedAt = now;
    }

    internal void Expire(DateTimeOffset now)
    {
        Response = OfferResponse.Expired;
        RespondedAt = now;
    }
}
