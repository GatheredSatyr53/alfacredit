using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Simulators;

/// <summary>Общее поведение симуляторов: задержка, инъекция сбоев, детерминированный генератор по ИНН.</summary>
internal abstract class SimulatorBase(CheckSource source, IOptions<SimulatorOptions> options, ILogger logger)
{
    protected CheckSource Source { get; } = source;

    protected async Task<T> ExecuteAsync<T>(Company company, Func<Company, Random, T> produce, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (settings.LatencyMs > 0)
        {
            await Task.Delay(Random.Shared.Next(settings.LatencyMs / 2, settings.LatencyMs + 1), cancellationToken);
        }

        if (settings.FailureRate > 0 && Random.Shared.NextDouble() < settings.FailureRate)
        {
            logger.LogWarning("Simulated outage of {Source} for INN {Inn}", Source, company.Inn);
            throw new ExternalSourceUnavailableException(Source, $"{Source.Title()}: сервис временно недоступен (имитация сбоя).");
        }

        return produce(company, new Random(StableSeed(company.Inn, Source)));
    }

    /// <summary>Один и тот же ИНН всегда даёт одни и те же данные: демо воспроизводимо.</summary>
    private static int StableSeed(string inn, CheckSource source)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var ch in inn)
            {
                hash = (hash ^ ch) * 16777619u;
            }

            hash = (hash ^ (uint)source) * 16777619u;
            return (int)(hash & 0x7fffffff);
        }
    }
}
