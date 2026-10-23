using System.ComponentModel.DataAnnotations;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Contracts.Applications;

/// <summary>Краткая карточка договора в заявке. Описано в docs/completion-flow.md.</summary>
/// <param name="Id">Идентификатор договора.</param>
/// <param name="Number">Номер договора, например KD-000001.</param>
/// <param name="Status">AwaitingSignature, Signed, Expired или Cancelled.</param>
/// <param name="Amount">Сумма кредита, ₽.</param>
/// <param name="TermMonths">Срок, мес.</param>
/// <param name="InterestRate">Годовая ставка, %.</param>
/// <param name="MonthlyPayment">Ежемесячный платёж, ₽.</param>
/// <param name="TotalPayment">Сумма всех платежей по графику, ₽.</param>
/// <param name="TotalInterest">Переплата за весь срок, ₽.</param>
/// <param name="DisbursementAccount">Расчётный счёт для зачисления.</param>
/// <param name="CreatedAt">Сформирован (UTC).</param>
/// <param name="SigningDeadline">До какого момента можно подписать (UTC).</param>
/// <param name="SignedAt">Когда подписан (UTC); null, пока не подписан.</param>
/// <param name="Signing">Состояние подписания кодом подтверждения.</param>
/// <param name="DocumentUrl">Печатная форма договора (HTML).</param>
public sealed record ContractSummaryDto(
    Guid Id,
    string Number,
    ContractStatus Status,
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    decimal MonthlyPayment,
    decimal TotalPayment,
    decimal TotalInterest,
    string DisbursementAccount,
    DateTimeOffset CreatedAt,
    DateTimeOffset SigningDeadline,
    DateTimeOffset? SignedAt,
    ContractSigningDto Signing,
    string DocumentUrl);

/// <summary>Состояние подписания кодом подтверждения.</summary>
/// <param name="CodeRequestedAt">Когда запрошен действующий код; null, если код не запрашивали.</param>
/// <param name="CodeExpiresAt">До какого момента действует код.</param>
/// <param name="AttemptsLeft">Сколько попыток ввода осталось для текущего кода.</param>
/// <param name="CanRequestCode">Можно ли запросить код: договор ожидает подписи и срок не истёк.</param>
public sealed record ContractSigningDto(DateTimeOffset? CodeRequestedAt, DateTimeOffset? CodeExpiresAt, int AttemptsLeft, bool CanRequestCode);

/// <summary>Кредитор.</summary>
public sealed record ContractPartyDto(string Name);

/// <summary>Заёмщик.</summary>
public sealed record ContractBorrowerDto(Guid CompanyId, string Name, string Inn, string Ogrn, string Address, string HeadName);

/// <summary>Кто подписал договор.</summary>
public sealed record ContractSignerDto(Guid UserId, string FullName);

/// <summary>Строка графика платежей.</summary>
/// <param name="Number">Номер платежа с 1.</param>
/// <param name="DueDate">Дата платежа.</param>
/// <param name="Payment">Платёж, ₽.</param>
/// <param name="Principal">Основной долг, ₽.</param>
/// <param name="Interest">Проценты, ₽.</param>
/// <param name="Balance">Остаток долга после платежа, ₽.</param>
public sealed record ScheduleRowDto(int Number, DateOnly DueDate, decimal Payment, decimal Principal, decimal Interest, decimal Balance);

/// <summary>
/// Полный договор: стороны, условия, подпись и график платежей. SignedBy и SignatureMethod заполняются после подписания;
/// Schedule это аннуитетный график, даты в нём ориентировочные до выдачи средств.
/// </summary>
public sealed record ContractDto(
    Guid Id,
    string Number,
    ContractStatus Status,
    Guid ApplicationId,
    string ApplicationNumber,
    ContractPartyDto Lender,
    ContractBorrowerDto Borrower,
    ApplicationProductDto Product,
    LoanPurpose Purpose,
    string PurposeTitle,
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    decimal MonthlyPayment,
    decimal TotalPayment,
    decimal TotalInterest,
    string DisbursementAccount,
    DateTimeOffset CreatedAt,
    DateTimeOffset SigningDeadline,
    DateTimeOffset? SignedAt,
    ContractSignerDto? SignedBy,
    string? SignatureMethod,
    ContractSigningDto Signing,
    IReadOnlyList<ScheduleRowDto> Schedule,
    string DocumentUrl);

/// <summary>
/// Выдача средств по договору. Status Completed: средства зачислены, PaymentOrderNumber содержит номер платёжного поручения;
/// Failed: банковское ядро не приняло перевод (Error с причиной), попытка повторяется автоматически.
/// </summary>
public sealed record DisbursementDto(
    Guid Id,
    decimal Amount,
    string Account,
    string? PaymentOrderNumber,
    DisbursementStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

/// <summary>Ответ на запрос кода подтверждения.</summary>
/// <param name="ContractNumber">Номер договора.</param>
/// <param name="DeliveredVia">Куда доставлен код: Feed (лента событий кабинета, имитация SMS).</param>
/// <param name="CodeExpiresAt">До какого момента действует код (UTC).</param>
/// <param name="AttemptsLeft">Сколько попыток ввода доступно.</param>
/// <param name="DemoCode">Сам код; только в демо-режиме (Contracts:RevealCodeInResponse), иначе null.</param>
public sealed record SignCodeResponse(string ContractNumber, string DeliveredVia, DateTimeOffset CodeExpiresAt, int AttemptsLeft, string? DemoCode);

/// <summary>Подписание договора кодом подтверждения.</summary>
public sealed class SignContractRequest
{
    /// <summary>Код из ленты событий (6 цифр).</summary>
    /// <example>483921</example>
    [Required(ErrorMessage = "Укажите код подтверждения.")]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Код подтверждения состоит из 6 цифр.")]
    public string? Code { get; init; }
}
