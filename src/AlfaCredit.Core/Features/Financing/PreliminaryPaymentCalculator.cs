using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Core.Features.Financing;

/// <summary>
/// Предварительный ежемесячный платёж по продукту для суммы и срока, которые клиент вводит в форме заявки.
/// Считается по базовой ставке продукта до проверки заявки; итоговую ставку банк назначает по оценке риска.
/// </summary>
/// <param name="Amount">Сумма, для которой сделан расчёт, ₽.</param>
/// <param name="TermMonths">Срок, для которого сделан расчёт, мес.</param>
/// <param name="InterestRate">Базовая годовая ставка продукта, %.</param>
/// <param name="MonthlyPayment">Аннуитетный платёж, ₽; null, если расчёт недоступен.</param>
/// <param name="TotalPayment">Сумма всех платежей по графику, ₽; null, если расчёт недоступен.</param>
/// <param name="TotalInterest">Переплата за весь срок, ₽; null, если расчёт недоступен.</param>
/// <param name="Notes">Оговорка к расчёту или причины, по которым он недоступен.</param>
public sealed record PreliminaryPayment(
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    decimal? MonthlyPayment,
    decimal? TotalPayment,
    decimal? TotalInterest,
    IReadOnlyList<PreviewNote> Notes)
{
    /// <summary>Платёж посчитан: продукт срочный, сумма и срок в его диапазоне.</summary>
    public bool IsAvailable => MonthlyPayment is not null;
}

public static class PreliminaryPaymentCalculator
{
    /// <summary>Код оговорки у доступного расчёта.</summary>
    public const string PreliminaryCode = "preliminary";

    /// <summary>
    /// Платёж, сумма всех платежей и переплата по базовой ставке продукта: тот же аннуитетный график, что в предварительном
    /// подборе, предложении банка и договоре. Для овердрафта и кредитной линии расчёт недоступен (<c>limit_product</c>),
    /// для суммы или срока вне диапазона продукта возвращаются причины с теми же кодами, что в подборе.
    /// </summary>
    public static PreliminaryPayment Calculate(CreditProduct product, decimal amount, int termMonths)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(termMonths);

        var reasons = Reasons(product, amount, termMonths).ToArray();
        if (reasons.Length > 0)
        {
            return new PreliminaryPayment(amount, termMonths, product.BaseRate, null, null, null, reasons);
        }

        var payment = LoanMath.MonthlyPayment(amount, product.BaseRate, termMonths);
        var totals = LoanMath.Totals(amount, product.BaseRate, termMonths);
        var note = new PreviewNote(
            PreliminaryCode,
            $"Предварительный расчёт по базовой ставке {Money.FormatRate(product.BaseRate)} годовых: {Money.FormatRub(Money.WholeRubles(payment))} в месяц, " +
            $"переплата {Money.FormatRub(Money.WholeRubles(totals.TotalInterest))} за {RussianPlural.Count(termMonths, "месяц", "месяца", "месяцев")}. " +
            "Итоговая ставка зависит от оценки риска: платёж в предложении банка может отличаться.");

        return new PreliminaryPayment(amount, termMonths, product.BaseRate, payment, totals.TotalPayment, totals.TotalInterest, [note]);
    }

    private static IEnumerable<PreviewNote> Reasons(CreditProduct product, decimal amount, int termMonths)
    {
        if (!product.Type.HasAnnuitySchedule())
        {
            yield return new PreviewNote(
                "limit_product",
                $"«{product.Name}»: проценты начисляются только за дни использования лимита, ежемесячный платёж заранее не определён.");
            yield break;
        }

        if (amount < product.MinAmount)
        {
            yield return new PreviewNote("amount_below_min", $"Минимальная сумма продукта {Money.FormatRub(product.MinAmount)}.");
        }
        else if (amount > product.MaxAmount)
        {
            yield return new PreviewNote("amount_above_max", $"Максимальная сумма продукта {Money.FormatRub(product.MaxAmount)}.");
        }

        if (!product.AllowsTerm(termMonths))
        {
            yield return new PreviewNote("term_out_of_range", $"Срок должен быть от {product.MinTermMonths} до {product.MaxTermMonths} мес.");
        }
    }
}
