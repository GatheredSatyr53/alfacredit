using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Кредитная заявка целиком: параметры, статус, решение, предложение, доступные действия и история.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Number">Человекочитаемый номер.</param>
/// <param name="Status">Текущий статус с текстами для интерфейса.</param>
/// <param name="Company">Компания-заявитель.</param>
/// <param name="Product">Выбранный продукт.</param>
/// <param name="Amount">Запрошенная сумма, ₽.</param>
/// <param name="TermMonths">Запрошенный срок, мес.</param>
/// <param name="Purpose">Цель финансирования.</param>
/// <param name="PurposeTitle">Название цели для интерфейса.</param>
/// <param name="PurposeDescription">Описание цели от клиента.</param>
/// <param name="NeedByDate">К какой дате клиенту нужны деньги; null, если дата не указана (в том числе у заявок, созданных до появления поля).</param>
/// <param name="CreatedByUserId">Кто подал заявку.</param>
/// <param name="CreatedAt">Создана (UTC).</param>
/// <param name="UpdatedAt">Последнее изменение (UTC).</param>
/// <param name="AvailableActions">Действия, доступные текущему пользователю: cancel, acceptOffer, declineOffer и другие.</param>
/// <param name="Decision">Последнее решение банка; null, пока решения нет.</param>
/// <param name="Offer">Действующее предложение банка; null, если его нет.</param>
/// <param name="Contract">Договор, сформированный после принятия предложения; null до этого момента. См. docs/completion-flow.md.</param>
/// <param name="Disbursement">Последняя попытка выдачи средств; null, пока выдача не выполнялась.</param>
/// <param name="Scoring">Результат скоринга; заполняется только для сотрудников банка.</param>
/// <param name="Checks">Внешние проверки; заполняются только для сотрудников банка.</param>
/// <param name="History">История статусов от создания к текущему.</param>
public sealed record ApplicationDto(
    Guid Id,
    string Number,
    ApplicationStatusDto Status,
    ApplicationCompanyDto Company,
    ApplicationProductDto Product,
    decimal Amount,
    int TermMonths,
    LoanPurpose Purpose,
    string PurposeTitle,
    string? PurposeDescription,
    DateOnly? NeedByDate,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> AvailableActions,
    DecisionDto? Decision,
    OfferDto? Offer,
    ContractSummaryDto? Contract,
    DisbursementDto? Disbursement,
    ScoringDto? Scoring,
    IReadOnlyList<ExternalCheckDto>? Checks,
    IReadOnlyList<ApplicationHistoryEntryDto> History);

/// <summary>Краткая карточка заявки для списков.</summary>
public sealed record ApplicationSummaryDto(
    Guid Id,
    string Number,
    ApplicationStatusDto Status,
    ApplicationCompanyDto Company,
    ApplicationProductDto Product,
    decimal Amount,
    int TermMonths,
    LoanPurpose Purpose,
    string PurposeTitle,
    DateOnly? NeedByDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Компания в заявке.</summary>
public sealed record ApplicationCompanyDto(Guid Id, string Name, string Inn);

/// <summary>Продукт в заявке.</summary>
public sealed record ApplicationProductDto(string Code, string Name);

/// <summary>Запись истории статусов.</summary>
/// <param name="FromStatus">Предыдущий статус; пусто для создания.</param>
/// <param name="ToStatus">Новый статус.</param>
/// <param name="ToStatusTitle">Название нового статуса.</param>
/// <param name="Actor">Кто выполнил действие: Client, Manager или System.</param>
/// <param name="ActorUserId">Пользователь, если действие выполнил человек.</param>
/// <param name="Comment">Комментарий к переходу.</param>
/// <param name="OccurredAt">Когда (UTC).</param>
public sealed record ApplicationHistoryEntryDto(
    ApplicationStatus? FromStatus,
    ApplicationStatus ToStatus,
    string ToStatusTitle,
    ActorType Actor,
    Guid? ActorUserId,
    string? Comment,
    DateTimeOffset OccurredAt);
