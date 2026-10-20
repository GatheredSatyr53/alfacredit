using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Features.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Processing;

/// <summary>
/// Фоновый исполнитель конвейера: несколько циклов забирают задания из базы (SKIP LOCKED) и выполняют этапы.
/// Отдельный цикл закрывает просроченные предложения.
/// </summary>
public sealed class ProcessingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<ProcessingOptions> options,
    TimeProvider clock,
    ILogger<ProcessingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        logger.LogInformation("Processing worker started: {Workers} loops, poll {Poll} ms, stage delay {Delay} s", settings.WorkerCount, settings.PollIntervalMs, settings.StageDelaySeconds);

        var loops = Enumerable.Range(0, settings.WorkerCount)
            .Select(index => RunJobLoopAsync(index, stoppingToken))
            .Append(RunOfferExpiryLoopAsync(stoppingToken));

        await Task.WhenAll(loops);
    }

    private async Task RunJobLoopAsync(int index, CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromMilliseconds(options.Value.PollIntervalMs);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await ProcessOneAsync(stoppingToken))
                {
                    await Task.Delay(poll, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker loop {Index} failed, pausing before retry", index);
                await SafeDelayAsync(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }

    private async Task<bool> ProcessOneAsync(CancellationToken stoppingToken)
    {
        Guid jobId;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var jobs = scope.ServiceProvider.GetRequiredService<IProcessingJobRepository>();
            var job = await jobs.ClaimNextAsync(clock.GetUtcNow(), stoppingToken);
            if (job is null)
            {
                return false;
            }

            jobId = job.Id;
            try
            {
                var pipeline = scope.ServiceProvider.GetRequiredService<ApplicationPipeline>();
                await pipeline.ExecuteAsync(job, stoppingToken);
                return true;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Job {JobId} ({Stage}) failed", job.Id, job.Stage);
            }
        }

        // Сбой обрабатываем в новом scope: контекст EF после исключения может быть в несогласованном состоянии
        await using var failureScope = scopeFactory.CreateAsyncScope();
        var handler = failureScope.ServiceProvider.GetRequiredService<ProcessingFailureHandler>();
        await handler.HandleAsync(jobId, new InvalidOperationException("Stage execution failed, see previous log entry."), stoppingToken);
        return true;
    }

    private async Task RunOfferExpiryLoopAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.OfferExpiryCheckSeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            await SafeDelayAsync(interval, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var expiry = scope.ServiceProvider.GetRequiredService<OfferExpiryService>();
                await expiry.ExpireAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Offer expiry check failed");
            }
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (OperationCanceledException)
        {
        }
    }
}
