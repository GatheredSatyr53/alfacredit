using AlfaCredit.Infrastructure.Scoring;
using AlfaCredit.Core.Features.Scoring;
using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Api.Setup;

/// <summary>
/// Доступность внешнего сервиса скоринга по GET /health. Недоступность понижает статус до Degraded, а не Unhealthy:
/// заявки всё равно обрабатываются встроенной моделью (RulesFallback), а в /health/ready видно, что сервис отвалился.
/// </summary>
public sealed class ScoringServiceHealthCheck(IHttpClientFactory httpClientFactory, IOptions<ScoringOptions> options, EmbeddedScoringService model) : IHealthCheck
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
                if (options.Value.ModelVersion == LogRegScoringService.ModelVersion)
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
                    var root = json.RootElement;
                    if (root.ValueKind != JsonValueKind.Object
                        || !root.TryGetProperty("model_version", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != model.ModelVersion
                        || !root.TryGetProperty("artifact_sha256", out var hash) || hash.ValueKind != JsonValueKind.String || hash.GetString() != model.ArtifactSha256)
                        return HealthCheckResult.Unhealthy("External and embedded scoring artifacts do not match.");
                }
                return HealthCheckResult.Healthy($"Сервис скоринга {baseAddress} отвечает.");
            }

            return HealthCheckResult.Degraded($"Сервис скоринга {baseAddress} ответил HTTP {(int)response.StatusCode}; работает встроенная модель.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return HealthCheckResult.Degraded($"Сервис скоринга {baseAddress} недоступен; работает встроенная модель.", ex);
        }
    }
}
