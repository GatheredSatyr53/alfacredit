using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Scoring;

/// <summary>Вызов внешнего сервиса скоринга по контракту docs/scoring-contract.md. При любой ошибке возвращает встроенную модель.</summary>
public sealed class HttpScoringService(
    HttpClient http,
    RuleBasedScoringService fallback,
    IOptions<ScoringOptions> options,
    ILogger<HttpScoringService> logger) : IScoringService
{
    public const string Endpoint = "api/v1/score";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken)
    {
        var attempts = options.Value.Retries + 1;
        Exception? last = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var response = await http.PostAsJsonAsync(Endpoint, request, JsonOptions, cancellationToken);
                response.EnsureSuccessStatusCode();
                var payload = await response.Content.ReadFromJsonAsync<ExternalScoringResponse>(JsonOptions, cancellationToken)
                    ?? throw new InvalidOperationException("Empty response from scoring service.");
                return Map(payload);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
            {
                last = ex;
                logger.LogWarning(ex, "Scoring service call failed (attempt {Attempt}/{Attempts})", attempt, attempts);
                if (attempt < attempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
                }
            }
        }

        logger.LogError(last, "Scoring service unavailable, falling back to built-in rules");
        var rules = await fallback.ScoreAsync(request, cancellationToken);
        return rules with
        {
            Source = ScoringSource.RulesFallback,
            Warnings = [.. rules.Warnings, "external_scoring_unavailable"],
        };
    }

    private static ScoringOutcome Map(ExternalScoringResponse payload)
    {
        if (!Enum.TryParse<RiskGrade>(payload.RiskGrade, ignoreCase: true, out var grade))
        {
            grade = RiskGrades.FromScore(payload.Score);
        }

        var factors = (payload.Factors ?? [])
            .Select(f => new ScoringFactor(f.Code ?? "factor", f.Title ?? f.Code ?? "Фактор", f.Impact, f.Value ?? string.Empty, f.ClientExplanation ?? string.Empty))
            .ToArray();

        return new ScoringOutcome(
            Math.Clamp(payload.Score, 0, 100),
            grade,
            payload.ProbabilityOfDefault,
            factors,
            payload.Warnings ?? [],
            string.IsNullOrWhiteSpace(payload.ModelVersion) ? "external" : payload.ModelVersion,
            ScoringSource.External);
    }

    private sealed record ExternalScoringResponse(
        Guid RequestId,
        string? ModelVersion,
        int Score,
        string? RiskGrade,
        decimal? ProbabilityOfDefault,
        List<ExternalFactor>? Factors,
        List<string>? Warnings);

    private sealed record ExternalFactor(string? Code, string? Title, int Impact, string? Value, string? ClientExplanation);
}
