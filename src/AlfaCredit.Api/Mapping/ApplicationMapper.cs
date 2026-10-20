using AlfaCredit.Api.Contracts.Applications;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Mapping;

public static class ApplicationMapper
{
    public static ApplicationDto ToDto(this CreditApplication application, Actor actor) => new(
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
        application.CreatedByUserId,
        application.CreatedAt,
        application.UpdatedAt,
        application.AvailableActions(actor.ActorType),
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
        application.CreatedAt,
        application.UpdatedAt);

    private static ApplicationHistoryEntryDto ToDto(ApplicationStatusHistory entry) => new(
        entry.FromStatus,
        entry.ToStatus,
        ApplicationStatusCatalog.Get(entry.ToStatus).Title,
        entry.ActorType,
        entry.ActorUserId,
        entry.Comment,
        entry.OccurredAt);
}
