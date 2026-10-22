using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Features.Scoring;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Скоринг заявки: встроенная балльная модель или внешний сервис по docs/scoring-contract.md.</summary>
public interface IScoringService
{
    Task<ScoringOutcome> ScoreAsync(ScoringRequest request, CancellationToken cancellationToken);
}
