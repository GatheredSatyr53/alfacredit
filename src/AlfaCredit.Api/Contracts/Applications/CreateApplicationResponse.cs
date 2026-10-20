using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Результат создания заявки.</summary>
/// <param name="Id">Идентификатор заявки для GET /api/applications/{id}.</param>
/// <param name="Number">Человекочитаемый номер, например AC-000001.</param>
/// <param name="Status">Статус после создания.</param>
public sealed record CreateApplicationResponse(Guid Id, string Number, ApplicationStatus Status);
