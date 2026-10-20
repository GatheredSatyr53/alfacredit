using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Applications;

/// <summary>Сценарии клиента с заявкой: создание, просмотр, список, отмена, ответ на предложение. Проверяет права участника.</summary>
public sealed class ApplicationService(
    ICreditApplicationRepository applications,
    ICompanyRepository companies,
    ICreditProductRepository products,
    IProcessingJobRepository jobs,
    FeedService feed,
    IApplicationNotifier notifier,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<ProcessingOptions> processingOptions,
    ILogger<ApplicationService> logger)
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

        var now = clock.GetUtcNow();
        var application = CreditApplication.Create(
            company!,
            product!,
            actor.UserId,
            command.Amount,
            command.TermMonths,
            command.Purpose,
            command.PurposeDescription,
            now);

        applications.Add(application);
        jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.Validate, now, processingOptions.Value.StageDelay, processingOptions.Value.MaxAttempts));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Номер заявки выдаёт база, поэтому событие ленты создаётся после первого сохранения
        feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Заявка {application.Number} принята",
            $"{product!.Name}, {Money.FormatRub(application.Amount)} на {application.TermMonths} мес. Автоматическая проверка начнётся через несколько секунд.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifySafeAsync(application, cancellationToken);

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
        var application = await GetForClientActionAsync(id, actor, "Отменить заявку может только клиент.", cancellationToken);
        application.Cancel(actor.UserId, reason, clock.GetUtcNow());
        feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Заявка {application.Number} отменена", "Вы можете подать новую заявку в любой момент.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifySafeAsync(application, cancellationToken);
        return application;
    }

    public async Task<CreditApplication> AcceptOfferAsync(Guid id, Actor actor, CancellationToken cancellationToken)
    {
        var application = await GetForClientActionAsync(id, actor, "Принять предложение может только клиент.", cancellationToken);
        var offer = application.AcceptOffer(actor.UserId, clock.GetUtcNow());
        feed.NotifyCompany(application, FeedItemType.ContractReady, $"Условия по заявке {application.Number} приняты",
            $"Банк готовит договор на {Money.FormatRub(offer.Amount)} на {offer.TermMonths} мес. под {offer.InterestRate:0.##} % годовых.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifySafeAsync(application, cancellationToken);
        return application;
    }

    public async Task<CreditApplication> DeclineOfferAsync(Guid id, Actor actor, string? reason, CancellationToken cancellationToken)
    {
        var application = await GetForClientActionAsync(id, actor, "Отклонить предложение может только клиент.", cancellationToken);
        application.DeclineOffer(actor.UserId, reason, clock.GetUtcNow());
        feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Предложение по заявке {application.Number} отклонено", "Вы можете подать новую заявку на других условиях.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await NotifySafeAsync(application, cancellationToken);
        return application;
    }

    private async Task<CreditApplication> GetForClientActionAsync(Guid id, Actor actor, string forbiddenMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsClient)
        {
            throw new ForbiddenException(forbiddenMessage);
        }

        return await GetAsync(id, actor, cancellationToken);
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

    private async Task NotifySafeAsync(CreditApplication application, CancellationToken cancellationToken)
    {
        try
        {
            await notifier.ApplicationChangedAsync(
                new ApplicationChangedNotification(
                    application.Id,
                    application.Number,
                    application.CompanyId,
                    application.Status,
                    ApplicationStatusCatalog.Get(application.Status).Title,
                    application.UpdatedAt),
                cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Realtime notification failed for application {Number}", application.Number);
        }
    }
}
