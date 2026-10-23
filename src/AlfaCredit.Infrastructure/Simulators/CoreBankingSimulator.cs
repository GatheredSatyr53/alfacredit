using System.Collections.Concurrent;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Simulators;

/// <summary>
/// Банковское ядро: моделирует перевод суммы кредита на расчётный счёт с задержкой и настраиваемой вероятностью сбоя.
/// Идемпотентно по договору: повторный перевод по тому же договору возвращает прежнее платёжное поручение,
/// как это делает настоящее ядро по ключу идемпотентности.
/// </summary>
public sealed class CoreBankingSimulator(IOptions<SimulatorOptions> options, TimeProvider clock, ILogger<CoreBankingSimulator> logger) : IDisbursementGateway
{
    private readonly ConcurrentDictionary<Guid, DisbursementReceipt> _processed = new();

    public async Task<DisbursementReceipt> TransferAsync(LoanContract contract, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (_processed.TryGetValue(contract.Id, out var existing))
        {
            logger.LogInformation("Transfer for contract {Contract} already processed as {Order}: idempotent replay", contract.Number, existing.PaymentOrderNumber);
            return existing;
        }

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
        var receipt = _processed.GetOrAdd(contract.Id, new DisbursementReceipt($"PO-{now:yyyyMMdd}-{suffix}", now));
        logger.LogInformation("Simulated transfer of {Amount} to {Account} for contract {Contract}: payment order {Order}", contract.Amount, contract.DisbursementAccount, contract.Number, receipt.PaymentOrderNumber);
        return receipt;
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
