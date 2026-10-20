using AlfaCredit.Api.Contracts.Products;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>Каталог кредитных продуктов. Доступен без входа, чтобы показывать витрину до регистрации.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
[AllowAnonymous]
public sealed class ProductsController(ICreditProductRepository products) : ControllerBase
{
    /// <summary>Список активных кредитных продуктов.</summary>
    /// <response code="200">Продукты в порядке отображения на витрине.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ProductDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> List(CancellationToken cancellationToken)
    {
        var items = await products.ListActiveAsync(cancellationToken);
        return items.Select(p => p.ToDto()).ToArray();
    }

    /// <summary>Кредитный продукт по коду.</summary>
    /// <param name="code">Код продукта, например working-capital.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Продукт найден.</response>
    /// <response code="404">Продукт с таким кодом не существует или отключён.</response>
    [HttpGet("{code}")]
    [ProducesResponseType<ProductDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetByCode(string code, CancellationToken cancellationToken)
    {
        var product = await products.FindByCodeAsync(code, cancellationToken);
        return product is null || !product.IsActive
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Продукт не найден", detail: $"Кредитный продукт с кодом «{code}» не найден.")
            : product.ToDto();
    }
}
