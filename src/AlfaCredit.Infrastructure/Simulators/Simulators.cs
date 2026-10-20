using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure.Simulators;

internal sealed class EgrulSimulator(IOptions<SimulatorOptions> options, ILogger<EgrulSimulator> logger)
    : SimulatorBase(CheckSource.Egrul, options, logger), IEgrulClient
{
    public Task<EgrulResult> GetAsync(Company company, CancellationToken cancellationToken) =>
        ExecuteAsync(company, (c, random) =>
        {
            if (KnownProfiles.Egrul.TryGetValue(c.Inn, out var known))
            {
                return new EgrulResult(known.Status, known.HeadDisqualified, known.MassAddress, c.RegisteredAt, c.Okved);
            }

            var roll = random.NextDouble();
            var status = roll < 0.94 ? EgrulStatus.Active : roll < 0.98 ? EgrulStatus.Liquidating : EgrulStatus.Terminated;
            return new EgrulResult(status, random.NextDouble() < 0.02, random.NextDouble() < 0.08, c.RegisteredAt, c.Okved);
        }, cancellationToken);
}

internal sealed class FnsSimulator(IOptions<SimulatorOptions> options, ILogger<FnsSimulator> logger)
    : SimulatorBase(CheckSource.Fns, options, logger), IFnsClient
{
    public Task<FnsResult> GetAsync(Company company, CancellationToken cancellationToken) =>
        ExecuteAsync(company, (c, random) =>
        {
            if (KnownProfiles.Fns.TryGetValue(c.Inn, out var known))
            {
                return known;
            }

            var roll = random.NextDouble();
            var debt = roll < 0.7 ? 0 : roll < 0.95 ? random.Next(1, 60) * 1_000m : random.Next(100, 900) * 1_000m;
            var reported = Math.Round(c.AnnualRevenue * (decimal)(0.9 + random.NextDouble() * 0.15), 0);
            return new FnsResult(debt, debt > 500_000 && random.NextDouble() < 0.5, reported);
        }, cancellationToken);
}

internal sealed class CreditBureauSimulator(IOptions<SimulatorOptions> options, ILogger<CreditBureauSimulator> logger)
    : SimulatorBase(CheckSource.CreditBureau, options, logger), ICreditBureauClient
{
    public Task<CreditBureauResult> GetAsync(Company company, CancellationToken cancellationToken) =>
        ExecuteAsync(company, (c, random) =>
        {
            if (KnownProfiles.CreditBureau.TryGetValue(c.Inn, out var known))
            {
                return known;
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var ageMonths = c.GetBusinessAgeMonths(today);
            if (ageMonths < 6 || random.NextDouble() < 0.15)
            {
                return new CreditBureauResult(0, 0, 0, 0, 0, 0, 0, false);
            }

            var history = Math.Min(ageMonths, random.Next(6, 120));
            var active = random.Next(0, 4);
            var closed = random.Next(0, 6);
            var debt = active == 0 ? 0 : Math.Round(c.AnnualRevenue * (decimal)(0.05 + random.NextDouble() * 0.4), 0);
            var payments = c.MonthlyDebtPayments > 0 ? c.MonthlyDebtPayments : Math.Round(debt / 24, 0);
            var overdueRoll = random.NextDouble();
            var maxOverdue = overdueRoll < 0.7 ? 0 : overdueRoll < 0.9 ? random.Next(1, 31) : overdueRoll < 0.97 ? random.Next(31, 91) : random.Next(91, 200);
            var overdueCount = maxOverdue == 0 ? 0 : random.Next(1, 4);
            return new CreditBureauResult(history, active, closed, debt, payments, maxOverdue, overdueCount, maxOverdue > 150);
        }, cancellationToken);
}

internal sealed class FsspSimulator(IOptions<SimulatorOptions> options, ILogger<FsspSimulator> logger)
    : SimulatorBase(CheckSource.Fssp, options, logger), IFsspClient
{
    public Task<FsspResult> GetAsync(Company company, CancellationToken cancellationToken) =>
        ExecuteAsync(company, (c, random) =>
        {
            if (KnownProfiles.Fssp.TryGetValue(c.Inn, out var known))
            {
                return known;
            }

            if (random.NextDouble() < 0.85)
            {
                return new FsspResult(0, 0);
            }

            var count = random.Next(1, 4);
            return new FsspResult(count, random.Next(20, 1500) * 1_000m);
        }, cancellationToken);
}
