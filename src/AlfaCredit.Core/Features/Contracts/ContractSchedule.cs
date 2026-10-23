using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;

namespace AlfaCredit.Core.Features.Contracts;

/// <summary>График платежей по договору: отсчёт от даты выдачи, до выдачи от подписания или формирования договора (ориентировочно).</summary>
public static class ContractSchedule
{
    public static DateOnly FirstDueDate(CreditApplication application, LoanContract contract)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(contract);
        var anchor = application.CurrentDisbursement?.CompletedAt ?? contract.SignedAt ?? contract.CreatedAt;
        return DateOnly.FromDateTime(anchor.UtcDateTime).AddMonths(1);
    }

    public static IReadOnlyList<ScheduleRow> Build(CreditApplication application, LoanContract contract) =>
        LoanMath.Schedule(contract.Amount, contract.InterestRate, contract.TermMonths, FirstDueDate(application, contract));
}
