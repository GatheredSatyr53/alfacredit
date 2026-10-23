using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Feed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Processing;

/// <summary>Сбой этапа: повтор с задержкой, а после исчерпания попыток заявка уходит менеджеру, а не теряется.</summary>
public sealed class ProcessingFailureHandler(
    IProcessingJobRepository jobs,
    ICreditApplicationRepository applications,
    FeedService feed,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<ProcessingOptions> options,
    IOptions<DemoManagerOptions> demoManager,
    ILogger<ProcessingFailureHandler> logger)
{
    public async Task HandleAsync(Guid jobId, Exception exception, CancellationToken cancellationToken)
    {
        var job = await jobs.FindByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var delay = TimeSpan.FromSeconds(options.Value.RetryDelaySeconds * Math.Max(1, job.Attempts));
        var willRetry = job.MarkFailed(exception.Message, now, delay);

        if (willRetry)
        {
            logger.LogWarning(exception, "Job {JobId} ({Stage}) failed, attempt {Attempt}/{Max}, retry in {Delay}", job.Id, job.Stage, job.Attempts, job.MaxAttempts, delay);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        logger.LogError(exception, "Job {JobId} ({Stage}) failed permanently, routing application to manual review", job.Id, job.Stage);

        var application = await applications.FindByIdAsync(job.ApplicationId, cancellationToken);
        if (application is not null && !application.IsTerminal && application.Status is not (ApplicationStatus.ManualReview or ApplicationStatus.NeedsClarification))
        {
            if (ApplicationWorkflow.CanTransition(application.Status, ApplicationStatus.ManualReview, ActorType.System))
            {
                application.RouteToManualReview($"Автоматическая обработка не удалась на этапе {job.Stage}. Заявка передана менеджеру.", now);
                feed.NotifyCompany(application, FeedItemType.ManualReview, $"Заявка {application.Number} передана менеджеру", "Автоматическая обработка приостановлена, менеджер продолжит рассмотрение вручную.");
                await feed.NotifyManagersAsync(application, FeedItemType.ManualReview, $"Заявка {application.Number}: сбой автоматической обработки", $"Этап {job.Stage}: {exception.Message}", cancellationToken);
                if (demoManager.Value.Enabled)
                {
                    jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.ManagerDecision, now, demoManager.Value.Delay, options.Value.MaxAttempts));
                }
            }
            else
            {
                // Например, выдача по подписанному договору: статус не меняется, операцию завершит сотрудник банка
                feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Заявка {application.Number}: операция банка задерживается", "Автоматическая операция не завершилась, сотрудник банка завершит её вручную. Ничего делать не нужно.");
                await feed.NotifyManagersAsync(application, FeedItemType.ManualReview, $"Заявка {application.Number}: сбой на этапе {job.Stage}", $"Этап {job.Stage} не выполнен после {job.Attempts} попыток: {exception.Message}. Требуется участие сотрудника.", cancellationToken);
            }
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
