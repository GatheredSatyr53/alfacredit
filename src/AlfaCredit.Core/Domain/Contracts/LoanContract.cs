using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Decisions;

namespace AlfaCredit.Core.Domain.Contracts;

public enum ContractStatus
{
    /// <summary>Сформирован, ожидает подписи клиента.</summary>
    AwaitingSignature = 1,

    Signed = 2,

    /// <summary>Срок подписания истёк, заявка отменена системой.</summary>
    Expired = 3,

    /// <summary>Клиент отказался от подписания.</summary>
    Cancelled = 4,
}

/// <summary>
/// Кредитный договор по принятому предложению. Условия фиксируются в момент формирования.
/// Подписывается простой электронной подписью: кодом подтверждения, который доставляется в ленту кабинета (имитация SMS).
/// </summary>
public sealed class LoanContract
{
    public const int NumberMaxLength = 32;
    public const int CodeLength = 6;
    public const string SimpleSignatureMethod = "Простая электронная подпись: код подтверждения из кабинета (имитация SMS)";

    private LoanContract()
    {
    }

    public Guid Id { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid OfferId { get; private set; }

    /// <summary>Номер договора KD-000001, выдаёт база данных при сохранении.</summary>
    public string Number { get; private set; } = null!;

    public ContractStatus Status { get; private set; }

    public decimal Amount { get; private set; }

    public int TermMonths { get; private set; }

    /// <summary>Годовая ставка, %.</summary>
    public decimal InterestRate { get; private set; }

    public decimal MonthlyPayment { get; private set; }

    /// <summary>Сумма всех платежей по графику.</summary>
    public decimal TotalPayment { get; private set; }

    /// <summary>Переплата: проценты за весь срок.</summary>
    public decimal TotalInterest { get; private set; }

    public string LenderName { get; private set; } = string.Empty;

    public string BorrowerName { get; private set; } = string.Empty;

    public string BorrowerInn { get; private set; } = string.Empty;

    /// <summary>Расчётный счёт для зачисления, 20 цифр (синтетический).</summary>
    public string DisbursementAccount { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>До этого момента договор можно подписать; затем заявка отменяется системой.</summary>
    public DateTimeOffset SigningDeadline { get; private set; }

    public DateTimeOffset? SignedAt { get; private set; }

    public Guid? SignedByUserId { get; private set; }

    public string? SignedByName { get; private set; }

    public string? SignatureMethod { get; private set; }

    /// <summary>Действующий код подтверждения. Хранится открыто: это имитация SMS, код и так доставляется в ленту кабинета.</summary>
    public string? ConfirmationCode { get; private set; }

    public DateTimeOffset? CodeRequestedAt { get; private set; }

    public DateTimeOffset? CodeExpiresAt { get; private set; }

    public int CodeAttempts { get; private set; }

    public int CodeMaxAttempts { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public int AttemptsLeft => Math.Max(0, CodeMaxAttempts - CodeAttempts);

    public bool IsAwaitingSignature => Status == ContractStatus.AwaitingSignature;

    public bool IsSigningExpired(DateTimeOffset now) => IsAwaitingSignature && now >= SigningDeadline;

    public bool CanRequestCode(DateTimeOffset now) => IsAwaitingSignature && now < SigningDeadline;

    public static LoanContract Create(
        Guid applicationId,
        Offer offer,
        string lenderName,
        string borrowerName,
        string borrowerInn,
        string disbursementAccount,
        DateTimeOffset now,
        TimeSpan signingPeriod,
        int codeMaxAttempts)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentException.ThrowIfNullOrWhiteSpace(lenderName);
        ArgumentException.ThrowIfNullOrWhiteSpace(borrowerName);
        ArgumentException.ThrowIfNullOrWhiteSpace(disbursementAccount);

        // Итоги считаются по графику: даты платежей на суммы не влияют
        var schedule = LoanMath.Schedule(offer.Amount, offer.InterestRate, offer.TermMonths, DateOnly.FromDateTime(now.UtcDateTime).AddMonths(1));

        return new()
        {
            Id = Guid.CreateVersion7(),
            ApplicationId = applicationId,
            OfferId = offer.Id,
            Status = ContractStatus.AwaitingSignature,
            Amount = offer.Amount,
            TermMonths = offer.TermMonths,
            InterestRate = offer.InterestRate,
            MonthlyPayment = offer.MonthlyPayment,
            TotalPayment = schedule.Sum(r => r.Payment),
            TotalInterest = schedule.Sum(r => r.Interest),
            LenderName = lenderName.Trim(),
            BorrowerName = borrowerName.Trim(),
            BorrowerInn = borrowerInn,
            DisbursementAccount = disbursementAccount,
            CreatedAt = now,
            SigningDeadline = now + (signingPeriod <= TimeSpan.Zero ? TimeSpan.FromDays(5) : signingPeriod),
            CodeMaxAttempts = Math.Max(1, codeMaxAttempts),
            UpdatedAt = now,
        };
    }

    /// <summary>Выдаёт новый код подтверждения; прежний код и его попытки сбрасываются.</summary>
    public void RequestCode(string code, DateTimeOffset now, TimeSpan ttl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        EnsureSignable(now);

        ConfirmationCode = code;
        CodeRequestedAt = now;
        CodeExpiresAt = now + (ttl <= TimeSpan.Zero ? TimeSpan.FromMinutes(10) : ttl);
        CodeAttempts = 0;
        UpdatedAt = now;
    }

    /// <summary>Подписание кодом. Неверный код расходует попытку и бросает ValidationException по полю code.</summary>
    public void Sign(string? code, Guid userId, string signerName, DateTimeOffset now)
    {
        EnsureSignable(now);

        if (ConfirmationCode is null || CodeExpiresAt is null)
        {
            throw new ConflictException("Сначала запросите код подтверждения.");
        }

        if (now >= CodeExpiresAt)
        {
            throw new ConflictException("Срок действия кода подтверждения истёк. Запросите новый код.");
        }

        if (CodeAttempts >= CodeMaxAttempts)
        {
            throw new ConflictException("Попытки ввода кода исчерпаны. Запросите новый код.");
        }

        if (!string.Equals(code?.Trim(), ConfirmationCode, StringComparison.Ordinal))
        {
            CodeAttempts++;
            UpdatedAt = now;
            var message = AttemptsLeft > 0
                ? $"Неверный код подтверждения. Осталось попыток: {AttemptsLeft}."
                : "Неверный код подтверждения. Попытки исчерпаны, запросите новый код.";
            throw new ValidationException(new Dictionary<string, string[]> { ["code"] = [message] });
        }

        Status = ContractStatus.Signed;
        SignedAt = now;
        SignedByUserId = userId;
        SignedByName = string.IsNullOrWhiteSpace(signerName) ? userId.ToString() : signerName.Trim();
        SignatureMethod = SimpleSignatureMethod;
        ConfirmationCode = null;
        UpdatedAt = now;
    }

    internal void Cancel(DateTimeOffset now)
    {
        if (!IsAwaitingSignature)
        {
            return;
        }

        Status = ContractStatus.Cancelled;
        ConfirmationCode = null;
        UpdatedAt = now;
    }

    internal void Expire(DateTimeOffset now)
    {
        if (!IsAwaitingSignature)
        {
            return;
        }

        Status = ContractStatus.Expired;
        ConfirmationCode = null;
        UpdatedAt = now;
    }

    private void EnsureSignable(DateTimeOffset now)
    {
        switch (Status)
        {
            case ContractStatus.Signed:
                throw new ConflictException("Договор уже подписан.");
            case ContractStatus.Cancelled:
            case ContractStatus.Expired:
                throw new ConflictException("Договор больше не действует.");
        }

        if (now >= SigningDeadline)
        {
            throw new ConflictException($"Срок подписания договора истёк {SigningDeadline:dd.MM.yyyy}. Подайте новую заявку.");
        }
    }
}
