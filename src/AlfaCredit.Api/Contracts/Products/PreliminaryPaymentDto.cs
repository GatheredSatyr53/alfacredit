using AlfaCredit.Api.Contracts.Financing;

namespace AlfaCredit.Api.Contracts.Products;

/// <summary>
/// Предварительный ежемесячный платёж по продукту для суммы и срока из запроса. Считается по базовой ставке продукта
/// до проверки заявки; фактические условия банк назначает в предложении после оценки риска.
/// </summary>
/// <param name="Amount">Сумма, для которой сделан расчёт, ₽.</param>
/// <param name="TermMonths">Срок, для которого сделан расчёт, мес.</param>
/// <param name="InterestRate">Базовая годовая ставка продукта, %.</param>
/// <param name="Available">Платёж посчитан: продукт срочный, сумма и срок в его диапазоне. Иначе числа null, причины в notes.</param>
/// <param name="MonthlyPayment">Аннуитетный ежемесячный платёж, ₽.</param>
/// <param name="TotalPayment">Сумма всех платежей по графику, ₽.</param>
/// <param name="TotalInterest">Переплата за весь срок, ₽.</param>
/// <param name="Notes">Оговорка к расчёту (код preliminary) или причины, по которым он недоступен: limit_product, amount_below_min, amount_above_max, term_out_of_range.</param>
public sealed record PreliminaryPaymentDto(
    decimal Amount,
    int TermMonths,
    decimal InterestRate,
    bool Available,
    decimal? MonthlyPayment,
    decimal? TotalPayment,
    decimal? TotalInterest,
    IReadOnlyList<PreviewNoteDto> Notes);
