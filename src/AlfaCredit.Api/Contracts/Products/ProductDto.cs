using AlfaCredit.Core.Domain.Products;

namespace AlfaCredit.Api.Contracts.Products;

/// <summary>Кредитный продукт банка.</summary>
/// <param name="Code">Код продукта; передаётся в заявке как productCode.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание для клиента: кому и для чего подходит.</param>
/// <param name="Type">Тип продукта.</param>
/// <param name="MinAmount">Минимальная сумма, ₽.</param>
/// <param name="MaxAmount">Максимальная сумма, ₽.</param>
/// <param name="MinTermMonths">Минимальный срок, мес.</param>
/// <param name="MaxTermMonths">Максимальный срок, мес.</param>
/// <param name="BaseRate">Базовая годовая ставка, %. Итоговая ставка зависит от оценки риска.</param>
/// <param name="AllowedPurposes">Допустимые цели финансирования.</param>
/// <param name="MinBusinessAgeMonths">Минимальный возраст бизнеса, мес.</param>
/// <param name="RequiresCollateral">Требуется ли залог.</param>
/// <param name="PreliminaryPayment">Предварительный ежемесячный платёж для amount и termMonths из запроса; null, если они не переданы.</param>
public sealed record ProductDto(
    string Code,
    string Name,
    string Description,
    ProductType Type,
    decimal MinAmount,
    decimal MaxAmount,
    int MinTermMonths,
    int MaxTermMonths,
    decimal BaseRate,
    IReadOnlyList<LoanPurposeDto> AllowedPurposes,
    int MinBusinessAgeMonths,
    bool RequiresCollateral,
    PreliminaryPaymentDto? PreliminaryPayment);

/// <summary>Цель финансирования: код для API и название для интерфейса.</summary>
public sealed record LoanPurposeDto(LoanPurpose Code, string Title);
