using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>Предварительный подбор для компании клиента. Ничего не сохраняет и не запускает обработку.</summary>
public sealed class FinancingPreviewService(
    ICompanyRepository companies,
    ICreditProductRepository products,
    FinancingAdvisor advisor,
    TimeProvider clock,
    IOptions<FinancingPreviewOptions> options)
{
    public async Task<FinancingPreview> PreviewAsync(FinancingPreviewQuery query, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(actor);

        var errors = new ValidationErrors();

        var company = await companies.FindByIdAsync(query.CompanyId, cancellationToken);
        if (company is null)
        {
            errors.Add("companyId", "Компания не найдена.");
        }
        else if (!actor.IsBankEmployee && !company.HasMember(actor.UserId))
        {
            throw new ForbiddenException("Вы не являетесь представителем этой компании.");
        }

        if (query.Amount <= 0)
        {
            errors.Add("amount", "Сумма должна быть положительным числом.");
        }

        if (query.TermMonths is <= 0)
        {
            errors.Add("termMonths", "Срок должен быть положительным числом месяцев.");
        }

        var now = clock.GetUtcNow();
        NeedByDateRules.Validate(query.NeedByDate, BankCalendar.Today(now), errors, options.Value.MaxNeedByDays);
        errors.ThrowIfAny();

        var catalog = await products.ListActiveAsync(cancellationToken);
        return advisor.Build(company!, catalog, query, now);
    }
}
