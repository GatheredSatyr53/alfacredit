using AlfaCredit.Api.Contracts.Applications;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Scoring;

namespace AlfaCredit.Api.Mapping;

public static class ApplicationMapper
{
    public static ApplicationDto ToDto(this CreditApplication application, Actor actor) => application.ToDto(actor, DateTimeOffset.UtcNow);

    public static ApplicationDto ToDto(this CreditApplication application, Actor actor, DateTimeOffset now) => new(
        application.Id,
        application.Number,
        ApplicationStatusDto.From(application.Status),
        new ApplicationCompanyDto(application.Company.Id, application.Company.Name, application.Company.Inn),
        new ApplicationProductDto(application.Product.Code, application.Product.Name),
        application.Amount,
        application.TermMonths,
        application.Purpose,
        application.Purpose.Title(),
        application.PurposeDescription,
        application.NeedByDate,
        application.CreatedByUserId,
        application.CreatedAt,
        application.UpdatedAt,
        application.AvailableActions(actor.ActorType),
        application.LatestDecision?.ToDto(),
        application.CurrentOffer?.ToDto(),
        application.CurrentContract?.ToSummaryDto(now),
        application.CurrentDisbursement?.ToDto(),
        actor.IsBankEmployee ? application.LatestScoring?.ToDto() : null,
        actor.IsBankEmployee ? application.Checks.Select(ToDto).ToArray() : null,
        application.History.Select(ToDto).ToArray());

    public static ApplicationSummaryDto ToSummaryDto(this CreditApplication application) => new(
        application.Id,
        application.Number,
        ApplicationStatusDto.From(application.Status),
        new ApplicationCompanyDto(application.Company.Id, application.Company.Name, application.Company.Inn),
        new ApplicationProductDto(application.Product.Code, application.Product.Name),
        application.Amount,
        application.TermMonths,
        application.Purpose,
        application.Purpose.Title(),
        application.NeedByDate,
        application.CreatedAt,
        application.UpdatedAt);

    public static DecisionDto ToDto(this Decision decision) => new(
        decision.Type,
        decision.IsFinal,
        decision.DecidedBy,
        decision.IsSimulated,
        decision.RecommendedType,
        decision.ApprovedAmount,
        decision.ApprovedTermMonths,
        decision.InterestRate,
        decision.MonthlyPayment,
        decision.Conditions,
        decision.Reasons.Select(r => new DecisionReasonDto(r.Code, r.Title, r.ClientExplanation)).ToArray(),
        decision.DecidedAt);

    public static OfferDto ToDto(this Offer offer) => new(
        offer.Id,
        offer.Amount,
        offer.TermMonths,
        offer.InterestRate,
        offer.MonthlyPayment,
        offer.ExpiresAt,
        offer.Response,
        offer.RespondedAt);

    public static ScoringDto ToDto(this ScoringResult scoring) => new(
        scoring.Score,
        scoring.Grade,
        scoring.ProbabilityOfDefault,
        scoring.Factors.Select(f => new ScoringFactorDto(f.Code, f.Title, f.Impact, f.Value, f.ClientExplanation)).ToArray(),
        scoring.Warnings,
        scoring.MissingData,
        scoring.ModelVersion,
        scoring.Source,
        scoring.RequestId,
        scoring.DurationMs,
        scoring.ComputedAt);

    private static ExternalCheckDto ToDto(ExternalCheck check) => new(
        check.Source,
        check.Source.Title(),
        check.Status,
        check.Summary,
        check.RiskFlags,
        check.Attempts,
        check.CheckedAt,
        check.Error);

    private static ApplicationHistoryEntryDto ToDto(ApplicationStatusHistory entry) => new(
        entry.FromStatus,
        entry.ToStatus,
        ApplicationStatusCatalog.Get(entry.ToStatus).Title,
        entry.ActorType,
        entry.ActorUserId,
        entry.Comment,
        entry.OccurredAt);
}
