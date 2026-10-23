using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Features.Feed;
using Microsoft.Extensions.Logging;

namespace AlfaCredit.Core.Features.Processing;

/// <summary>Закрывает просроченное: заявки с истёкшими предложениями уходят в «предложение отклонено», с неподписанными в срок договорами в «отменена».</summary>
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

        foreach (var application in await applications.ListWithExpiredContractsAsync(now, cancellationToken))
        {
            if (!application.ExpireContractIfNeeded(now))
            {
                continue;
            }

            feed.NotifyCompany(application, FeedItemType.StatusChanged, $"Срок подписания договора по заявке {application.Number} истёк", "Заявка отменена. Вы можете подать новую заявку, банк рассмотрит её заново.");
            expired++;
        }

        if (expired > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Expired {Count} offers or unsigned contracts", expired);
        }

        return expired;
    }
}
