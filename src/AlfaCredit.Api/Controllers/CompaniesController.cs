using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Companies;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>Компании клиента. Клиент видит только свои компании, сотрудник банка все.</summary>
[ApiController]
[Route("api/companies")]
[Produces("application/json")]
[Authorize]
public sealed class CompaniesController(ICompanyRepository companies, TimeProvider clock) : ControllerBase
{
    /// <summary>Компании, от имени которых текущий пользователь может подавать заявки.</summary>
    /// <remarks>Для сотрудника банка возвращает все компании.</remarks>
    /// <response code="200">Список компаний.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CompanyDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List(CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var items = actor.IsBankEmployee
            ? await companies.ListAllAsync(cancellationToken)
            : await companies.ListForUserAsync(actor.UserId, cancellationToken);

        var today = BankCalendar.Today(clock.GetUtcNow());
        return items.Select(c => c.ToDto(actor, today)).ToArray();
    }

    /// <summary>Карточка компании.</summary>
    /// <response code="200">Компания найдена.</response>
    /// <response code="403">Пользователь не является представителем компании.</response>
    /// <response code="404">Компания не найдена.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<CompanyDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var company = await companies.FindByIdAsync(id, cancellationToken);
        if (company is null)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Компания не найдена");
        }

        if (!actor.IsBankEmployee && !company.HasMember(actor.UserId))
        {
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Доступ запрещён", detail: "Вы не являетесь представителем этой компании.");
        }

        var today = BankCalendar.Today(clock.GetUtcNow());
        return company.ToDto(actor, today);
    }
}
