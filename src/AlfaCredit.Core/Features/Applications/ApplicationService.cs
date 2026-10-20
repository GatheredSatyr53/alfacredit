using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;

namespace AlfaCredit.Core.Features.Applications;

/// <summary>Сценарии работы с заявкой: создание, просмотр, список, отмена. Проверяет права участника.</summary>
public sealed class ApplicationService(
    ICreditApplicationRepository applications,
    ICompanyRepository companies,
    ICreditProductRepository products,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<CreditApplication> CreateAsync(CreateApplicationCommand command, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsClient)
        {
            throw new ForbiddenException("Подавать заявки могут только представители компаний.");
        }

        var errors = new ValidationErrors();

        var company = await companies.FindByIdAsync(command.CompanyId, cancellationToken);
        if (company is null)
        {
            errors.Add("companyId", "Компания не найдена.");
        }
        else if (!company.HasMember(actor.UserId))
        {
            throw new ForbiddenException("Вы не являетесь представителем этой компании.");
        }

        var product = string.IsNullOrWhiteSpace(command.ProductCode)
            ? null
            : await products.FindByCodeAsync(command.ProductCode, cancellationToken);
        if (product is null)
        {
            errors.Add("productCode", $"Кредитный продукт с кодом «{command.ProductCode}» не найден.");
        }

        errors.ThrowIfAny();

        var application = CreditApplication.Create(
            company!,
            product!,
            actor.UserId,
            command.Amount,
            command.TermMonths,
            command.Purpose,
            command.PurposeDescription,
            clock.GetUtcNow());

        applications.Add(application);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return application;
    }

    public async Task<CreditApplication> GetAsync(Guid id, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        var application = await applications.FindByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"Заявка {id} не найдена.");

        await EnsureCanAccessAsync(application, actor, cancellationToken);
        return application;
    }

    public async Task<PagedResult<CreditApplication>> ListAsync(ApplicationListQuery query, Actor actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(actor);

        IReadOnlyCollection<Guid>? companyIds = null;
        if (!actor.IsBankEmployee)
        {
            companyIds = await companies.GetMemberCompanyIdsAsync(actor.UserId, cancellationToken);
            if (companyIds.Count == 0)
            {
                return new PagedResult<CreditApplication>([], query.NormalizedPage, query.NormalizedPageSize, 0);
            }
        }

        var filter = new ApplicationFilter(companyIds, query.Status, query.NormalizedPage, query.NormalizedPageSize);
        return await applications.ListAsync(filter, cancellationToken);
    }

    public async Task<CreditApplication> CancelAsync(Guid id, Actor actor, string? reason, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (!actor.IsClient)
        {
            throw new ForbiddenException("Отменить заявку может только клиент.");
        }

        var application = await GetAsync(id, actor, cancellationToken);
        application.Cancel(actor.UserId, reason, clock.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return application;
    }

    private async Task EnsureCanAccessAsync(CreditApplication application, Actor actor, CancellationToken cancellationToken)
    {
        if (actor.IsBankEmployee)
        {
            return;
        }

        if (!await companies.IsMemberAsync(application.CompanyId, actor.UserId, cancellationToken))
        {
            throw new ForbiddenException("У вас нет доступа к этой заявке.");
        }
    }
}
