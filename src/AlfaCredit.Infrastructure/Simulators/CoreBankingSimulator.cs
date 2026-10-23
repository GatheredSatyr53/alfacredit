using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Simulators;

/// <summary>Банковское ядро: моделирует перевод суммы кредита на расчётный счёт с задержкой и настраиваемой вероятностью сбоя.</summary>
internal sealed class CoreBankingSimulator(IOptions<SimulatorOptions> options, TimeProvider clock, ILogger<CoreBankingSimulator> logger) : IDisbursementGateway
{
    public async Task<DisbursementReceipt> TransferAsync(LoanContract contract, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var settings = options.Value;
        if (settings.LatencyMs > 0)
        {
            await Task.Delay(Random.Shared.Next(settings.LatencyMs / 2, settings.LatencyMs + 1), cancellationToken);
        }

        if (settings.FailureRate > 0 && Random.Shared.NextDouble() < settings.FailureRate)
        {
            logger.LogWarning("Simulated core banking outage for contract {Contract}", contract.Number);
            throw new DisbursementFailedException("Банковское ядро временно недоступно (имитация сбоя), перевод не выполнен.");
        }

        var now = clock.GetUtcNow();
        var suffix = new Random(StableSeed(contract.Id)).Next(1000, 10_000);
        var paymentOrder = $"PO-{now:yyyyMMdd}-{suffix}";
        logger.LogInformation("Simulated transfer of {Amount} to {Account} for contract {Contract}: payment order {Order}", contract.Amount, contract.DisbursementAccount, contract.Number, paymentOrder);
        return new DisbursementReceipt(paymentOrder, now);
    }

    private static int StableSeed(Guid id)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var b in id.ToByteArray())
            {
                hash = (hash ^ b) * 16777619u;
            }

            return (int)(hash & 0x7fffffff);
        }
    }
}
