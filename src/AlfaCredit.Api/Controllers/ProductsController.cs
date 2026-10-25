using AlfaCredit.Api.Contracts.Products;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Features.Financing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>
/// Каталог кредитных продуктов. Доступен без входа, чтобы показывать витрину до регистрации.
/// С параметрами amount и termMonths к каждому продукту добавляется предварительный ежемесячный платёж по базовой ставке.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class ProductsController(ICreditProductRepository products) : ControllerBase
{
    /// <summary>Список активных кредитных продуктов.</summary>
    /// <remarks>
    /// С параметрами amount и termMonths в каждом продукте заполняется preliminaryPayment: платёж, сумма всех платежей
    /// и переплата по базовой ставке для срочных кредитов; для овердрафта и кредитной линии расчёт недоступен,
    /// для суммы или срока вне диапазона продукта возвращаются причины. Без параметров preliminaryPayment = null.
    /// </remarks>
    /// <param name="payment">Сумма и срок для предварительного платежа; указываются вместе.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Продукты в порядке отображения на витрине.</response>
    /// <response code="400">Сумма или срок вне допустимых значений, или передан только один из параметров.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List([FromQuery] PreliminaryPaymentQuery payment, CancellationToken cancellationToken)
    {
        var terms = payment.RequestedTerms();
        var items = await products.ListActiveAsync(cancellationToken);
        return items.Select(p => p.ToDto(Calculate(p, terms))).ToArray();
    }

    /// <summary>Кредитный продукт по коду.</summary>
    /// <param name="code">Код продукта, например working-capital.</param>
    /// <param name="payment">Сумма и срок для предварительного платежа; указываются вместе.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Продукт найден; с параметрами amount и termMonths заполнен preliminaryPayment.</response>
    /// <response code="400">Сумма или срок вне допустимых значений, или передан только один из параметров.</response>
    /// <response code="404">Продукт с таким кодом не существует или отключён.</response>
    [HttpGet("{code}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetByCode(string code, [FromQuery] PreliminaryPaymentQuery payment, CancellationToken cancellationToken)
    {
        var terms = payment.RequestedTerms();
        var product = await products.FindByCodeAsync(code, cancellationToken);
        return product is null || !product.IsActive
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Продукт не найден", detail: $"Кредитный продукт с кодом «{code}» не найден.")
            : product.ToDto(Calculate(product, terms));
    }

    private static PreliminaryPayment? Calculate(CreditProduct product, (decimal Amount, int TermMonths)? terms) =>
        terms is { } t ? PreliminaryPaymentCalculator.Calculate(product, t.Amount, t.TermMonths) : null;
}
