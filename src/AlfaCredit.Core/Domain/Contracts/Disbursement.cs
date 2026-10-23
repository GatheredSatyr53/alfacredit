namespace AlfaCredit.Core.Domain.Contracts;

public enum DisbursementStatus
{
    Completed = 1,
    Failed = 2,
}

/// <summary>Выдача кредита: перевод на расчётный счёт компании через банковское ядро (в прототипе симулятор).</summary>
public sealed class Disbursement
{
    public const int ErrorMaxLength = 1000;

    private Disbursement()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ContractId { get; private set; }

    public decimal Amount { get; private set; }

    public string Account { get; private set; } = string.Empty;

    /// <summary>Номер платёжного поручения из банковского ядра.</summary>
    public string? PaymentOrderNumber { get; private set; }

    public DisbursementStatus Status { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? Error { get; private set; }

    public static Disbursement Completed(Guid applicationId, LoanContract contract, string paymentOrderNumber, DateTimeOffset requestedAt, DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentOrderNumber);
        return new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            ContractId = contract.Id,
            Amount = contract.Amount,
            Account = contract.DisbursementAccount,
            PaymentOrderNumber = paymentOrderNumber,
            Status = DisbursementStatus.Completed,
            RequestedAt = requestedAt,
            CompletedAt = completedAt,
        };
    }

    public static Disbursement Failed(Guid applicationId, LoanContract contract, string error, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            ContractId = contract.Id,
            Amount = contract.Amount,
            Account = contract.DisbursementAccount,
            Status = DisbursementStatus.Failed,
            RequestedAt = requestedAt,
            Error = string.IsNullOrWhiteSpace(error) ? "unknown error" : (error.Length > ErrorMaxLength ? error[..ErrorMaxLength] : error),
        };
    }
}
