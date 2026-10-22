using AlfaCredit.Infrastructure.Scoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Api.Setup;

/// <summary>
/// Доступность внешнего сервиса скоринга по GET /health. Недоступность понижает статус до Degraded, а не Unhealthy:
/// заявки всё равно обрабатываются встроенной моделью (RulesFallback), а в /health/ready видно, что сервис отвалился.
/// </summary>
public sealed class ScoringServiceHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ScoringOptions> options) : IHealthCheck
{
    public const string ClientName = "scoring-health";

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var baseAddress = options.Value.ResolveBaseAddress();
        try
        {
            using var client = httpClientFactory.CreateClient(ClientName);
            using var response = await client.GetAsync(new Uri(baseAddress, "health"), cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"Сервис скоринга {baseAddress} отвечает.");
            }

            return HealthCheckResult.Degraded($"Сервис скоринга {baseAddress} ответил HTTP {(int)response.StatusCode}; работает встроенная модель.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return HealthCheckResult.Degraded($"Сервис скоринга {baseAddress} недоступен; работает встроенная модель.", ex);
        }
    }
}
