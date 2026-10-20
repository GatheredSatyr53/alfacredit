using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Результат скоринга независимо от того, кто его посчитал: встроенные правила или внешний сервис.</summary>
public sealed record ScoringOutcome(
    int Score,
    RiskGrade Grade,
    decimal? ProbabilityOfDefault,
    IReadOnlyList<ScoringFactor> Factors,
    IReadOnlyList<string> Warnings,
    string ModelVersion,
    ScoringSource Source);

public interface IScoringService
{
    Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken);
}
