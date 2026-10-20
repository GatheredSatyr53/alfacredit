using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Features.Feed;
using Microsoft.Extensions.Logging;

namespace AlfaCredit.Core.Features.Processing;

/// <summary>Переводит заявки с истёкшими предложениями в «предложение отклонено».</summary>
public sealed class OfferExpiryService(ICreditApplicationRepository applications, FeedService feed, IUnitOfWork unitOfWork, TimeProvider clock, ILogger<OfferExpiryService> logger)
{
    public async Task<int> ExpireAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var expired = 0;
        foreach (var application in await applications.ListWithExpiredOffersAsync(now, cancellationToken))
        {
            if (!application.ExpireOfferIfNeeded(now))
            {
                continue;
            }

            feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Срок предложения по заявке {application.Number} истёк", "Вы можете подать новую заявку, банк рассмотрит её заново.");
            expired++;
        }

        if (expired > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Expired {Count} offers", expired);
        }

        return expired;
    }
}
