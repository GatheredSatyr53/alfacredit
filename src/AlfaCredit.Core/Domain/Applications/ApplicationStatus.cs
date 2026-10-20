namespace AlfaCredit.Core.Domain.Applications;

/// <summary>Статус кредитной заявки. Переходы описаны в <see cref="ApplicationWorkflow"/>, тексты в <see cref="ApplicationStatusCatalog"/>.</summary>
public enum ApplicationStatus
{
    Draft = 0,
    Submitted = 10,
    Validating = 20,
    NeedsClarification = 25,
    Enriching = 30,
    Scoring = 40,
    Deciding = 50,
    ManualReview = 55,
    Approved = 60,
    ApprovedWithConditions = 61,
    Rejected = 62,
    ContractSigning = 70,
    Signed = 80,
    Disbursed = 90,
    Cancelled = 100,
    OfferDeclined = 101,
}
