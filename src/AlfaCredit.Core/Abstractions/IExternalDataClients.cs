using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Внешний источник временно недоступен: конвейер повторит запрос.</summary>
public sealed class ExternalSourceUnavailableException(CheckSource source, string message) : Exception(message)
{
    public CheckSource CheckSource { get; } = source;
}

public interface IEgrulClient
{
    Task<EgrulResult> GetAsync(Company company, CancellationToken cancellationToken);
}

public interface IFnsClient
{
    Task<FnsResult> GetAsync(Company company, CancellationToken cancellationToken);
}

public interface ICreditBureauClient
{
    Task<CreditBureauResult> GetAsync(Company company, CancellationToken cancellationToken);
}

public interface IFsspClient
{
    Task<FsspResult> GetAsync(Company company, CancellationToken cancellationToken);
}
