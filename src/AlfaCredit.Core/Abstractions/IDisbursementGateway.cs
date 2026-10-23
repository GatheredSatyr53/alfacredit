using AlfaCredit.Core.Domain.Contracts;

namespace AlfaCredit.Core.Abstractions;

/// <summary>Подтверждение перевода от банковского ядра.</summary>
public sealed record DisbursementReceipt(string PaymentOrderNumber, DateTimeOffset ProcessedAt);

/// <summary>Банковское ядро не выполнило перевод: временный сбой, задание конвейера повторит попытку.</summary>
public sealed class DisbursementFailedException(string message) : Exception(message);

/// <summary>Банковское ядро: перевод суммы кредита на расчётный счёт заёмщика. В прототипе симулятор.</summary>
public interface IDisbursementGateway
{
    Task<DisbursementReceipt> TransferAsync(LoanContract contract, CancellationToken cancellationToken);
}

/// <summary>Генератор кодов подтверждения для подписания; в тестах подменяется предсказуемым.</summary>
public interface ISigningCodeGenerator
{
    string Next();
}
