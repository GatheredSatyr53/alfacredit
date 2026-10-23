using AlfaCredit.Core.Abstractions;
using AlfaCredit.Infrastructure.Simulators;
using AlfaCredit.UnitTests.Applications;
using AlfaCredit.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class CoreBankingSimulatorTests
{
    [Fact]
    public async Task Transfer_is_idempotent_per_contract()
    {
        var (_, contract) = LoanContractTests.SigningApplication();
        var (_, other) = LoanContractTests.SigningApplication();
        IDisbursementGateway bank = new CoreBankingSimulator(
            Options.Create(new SimulatorOptions { LatencyMs = 0, FailureRate = 0 }),
            new FixedTimeProvider(TestData.Now),
            NullLogger<CoreBankingSimulator>.Instance);

        var first = await bank.TransferAsync(contract, CancellationToken.None);
        var replay = await bank.TransferAsync(contract, CancellationToken.None);
        var another = await bank.TransferAsync(other, CancellationToken.None);

        first.PaymentOrderNumber.ShouldStartWith("PO-20261020-");
        replay.ShouldBe(first, "повтор по тому же договору не создаёт второе платёжное поручение");
        another.PaymentOrderNumber.ShouldNotBe(first.PaymentOrderNumber);
    }
}
