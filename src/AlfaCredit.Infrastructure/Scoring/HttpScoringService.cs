using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Scoring;

/// <summary>
/// Вызов внешнего сервиса скоринга по контракту docs/scoring-contract.md: таймаут на запрос, ограниченные повторы
/// при 5xx, таймауте и обрыве связи, без повторов при 4xx. Если ответа так и нет, считает встроенная модель
/// с пометкой RulesFallback: заявка никогда не зависает из-за скоринга.
/// </summary>
public sealed class HttpScoringService(
    HttpClient http,
    IOptions<ScoringOptions> options,
    ILogger<HttpScoringService> logger) : IScoringService
{
    public const string Endpoint = "api/v1/score";
    public const string UnavailableWarning = "external_scoring_unavailable";
    private const int BodyPreviewLength = 500;

    /// <summary>JSON по контракту: snake_case, перечисления строками, метки времени в UTC с суффиксом Z; числа из строк принимаются.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new JsonStringEnumConverter(), new UtcTimestampConverter() },
    };

    public static string SerializeRequest(ScoringRequest request) => JsonSerializer.Serialize(request, JsonOptions);

    public async Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var settings = options.Value;
        var attempts = settings.Retries + 1;
        var failure = "no attempts";

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                using var response = await http.PostAsJsonAsync(Endpoint, request, JsonOptions, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var payload = await response.Content.ReadFromJsonAsync<ExternalScoringResponse>(JsonOptions, cancellationToken)
                        ?? throw new JsonException("Empty response body.");
                    var outcome = Map(payload);
                    logger.LogInformation(
                        "Scoring service answered request {RequestId}: {Score} points, grade {Grade}, model {Model}, attempt {Attempt}",
                        request.RequestId, outcome.Score, outcome.Grade, outcome.ModelVersion, attempt);
                    return outcome;
                }

                var status = (int)response.StatusCode;
                var body = await ReadPreviewAsync(response, cancellationToken);
                failure = $"HTTP {status}";
                if (status is >= 400 and < 500)
                {
                    // Сервис отверг запрос: повтор того же payload не поможет
                    logger.LogError("Scoring service rejected request {RequestId} with HTTP {Status}: {Body}", request.RequestId, status, body);
                    break;
                }

                logger.LogWarning("Scoring service returned HTTP {Status} for request {RequestId} (attempt {Attempt}/{Attempts}): {Body}", status, request.RequestId, attempt, attempts, body);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
            {
                failure = ex is TaskCanceledException ? $"timeout {settings.TimeoutSeconds} s" : ex.Message;
                logger.LogWarning(ex, "Scoring service call failed for request {RequestId} (attempt {Attempt}/{Attempts})", request.RequestId, attempt, attempts);
            }

            if (attempt < attempts)
            {
                await Task.Delay(TimeSpan.FromMilliseconds((long)settings.RetryDelayMs * attempt), cancellationToken);
            }
        }

        logger.LogError("Scoring service unavailable for request {RequestId} ({Failure}), falling back to built-in rules", request.RequestId, failure);
        var rules = RuleBasedScoringService.Score(request);
        return rules with
        {
            Source = ScoringSource.RulesFallback,
            Warnings = [.. rules.Warnings, UnavailableWarning],
        };
    }

    private static ScoringOutcome Map(ExternalScoringResponse payload)
    {
        var rawScore = payload.Score ?? throw new JsonException("Field 'score' is required.");
        var score = Math.Clamp((int)Math.Round(rawScore, MidpointRounding.AwayFromZero), 0, 100);
        if (!Enum.TryParse<RiskGrade>(payload.RiskGrade?.Trim(), ignoreCase: true, out var grade))
        {
            grade = RiskGrades.FromScore(score);
        }

        var factors = (payload.Factors ?? [])
            .OfType<ExternalFactor>()
            .Select(f => new ScoringFactor(
                string.IsNullOrWhiteSpace(f.Code) ? "factor" : f.Code.Trim(),
                string.IsNullOrWhiteSpace(f.Title) ? (string.IsNullOrWhiteSpace(f.Code) ? "Фактор" : f.Code.Trim()) : f.Title.Trim(),
                (int)Math.Round(f.Impact ?? 0, MidpointRounding.AwayFromZero),
                AsText(f.Value),
                f.ClientExplanation?.Trim() ?? string.Empty))
            .ToArray();

        return new ScoringOutcome(
            score,
            grade,
            payload.ProbabilityOfDefault is { } pd ? Math.Clamp((decimal)pd, 0m, 1m) : null,
            factors,
            AsTextList(payload.Warnings),
            AsTextList(payload.MissingData),
            string.IsNullOrWhiteSpace(payload.ModelVersion) ? "external" : payload.ModelVersion.Trim(),
            ScoringSource.External);
    }

    private static string AsText(JsonElement? element) => element is { } value
        ? value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
            _ => value.GetRawText(),
        }
        : string.Empty;

    private static IReadOnlyList<string> AsTextList(List<JsonElement>? elements) =>
        (elements ?? []).Select(e => AsText(e)).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

    private static async Task<string> ReadPreviewAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return text.Length > BodyPreviewLength ? text[..BodyPreviewLength] + "…" : text;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return $"<unreadable body: {ex.Message}>";
        }
    }

    /// <summary>Ответ сервиса. Типы намеренно мягкие: лишние поля игнорируются, числа могут прийти строкой, списки могут отсутствовать.</summary>
    private sealed record ExternalScoringResponse(
        JsonElement? RequestId,
        string? ModelVersion,
        double? Score,
        string? RiskGrade,
        double? ProbabilityOfDefault,
        List<ExternalFactor?>? Factors,
        List<JsonElement>? Warnings,
        List<JsonElement>? MissingData);

    private sealed record ExternalFactor(string? Code, string? Title, double? Impact, JsonElement? Value, string? ClientExplanation);
}
