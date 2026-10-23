using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>Три вопроса клиента: на что, сколько и к какому дню; срок возврата по желанию.</summary>
public sealed record FinancingPreviewQuery(Guid CompanyId, decimal Amount, LoanPurpose Purpose, DateOnly NeedByDate, int? TermMonths);
