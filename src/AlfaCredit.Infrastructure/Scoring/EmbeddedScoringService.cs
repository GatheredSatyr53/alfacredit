using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Scoring;

/// <summary>The selected local model is also used when the external service is unavailable.</summary>
public sealed class EmbeddedScoringService : IScoringService
{
    private readonly LogRegScoringService.Artifact? _artifact;
    public string ModelVersion { get; }
    public string? ArtifactSha256 { get; }

    public EmbeddedScoringService(IOptions<ScoringOptions> options)
    {
        ModelVersion = options.Value.ModelVersion;
        if (ModelVersion == LogRegScoringService.ModelVersion)
        {
            var path = options.Value.ArtifactPath ?? Path.Combine(AppContext.BaseDirectory, "Scoring", $"{ModelVersion}.json");
            _artifact = LogRegScoringService.Artifact.Load(path);
            ArtifactSha256 = _artifact.Sha256;
        }
        else if (ModelVersion != RuleBasedScoringService.ModelVersion)
            throw new InvalidOperationException($"Unsupported scoring model: {ModelVersion}");
    }

    public Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Score(request));
    }

    public ScoringOutcome Score(ScoringRequest request) => _artifact is null
        ? RuleBasedScoringService.Score(request) : LogRegScoringService.Score(request, _artifact);

    public int ScoreFromPd(double pd) => _artifact is null
        ? throw new InvalidOperationException("The selected model is not logistic regression")
        : LogRegScoringService.ScoreFromPd(pd, _artifact);
}

/// <summary>Resolve and validate the fallback artifact before the API starts accepting requests.</summary>
public sealed class ScoringModelStartupCheck(EmbeddedScoringService model) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = model.ModelVersion;
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
