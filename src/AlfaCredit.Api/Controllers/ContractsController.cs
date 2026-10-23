using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Applications;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Features.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>Договор по заявке: просмотр, печатная форма, подписание кодом подтверждения. Сценарий описан в docs/completion-flow.md.</summary>
[ApiController]
[Route("api/applications/{id:guid}/contract")]
[Produces("application/json")]
[Authorize]
public sealed class ContractsController(ContractService contracts, TimeProvider clock) : ControllerBase
{
    /// <summary>Договор по заявке: стороны, условия, подпись и график платежей.</summary>
    /// <remarks>Клиент видит договоры своих компаний, сотрудник банка любые. Договор появляется после принятия предложения.</remarks>
    /// <response code="200">Договор найден.</response>
    /// <response code="403">Нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена или договор ещё не сформирован.</response>
    [HttpGet]
    [ProducesResponseType<ContractDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (application, contract) = await contracts.GetAsync(id, User.ToActor(), cancellationToken);
        return contract.ToDto(application, clock.GetUtcNow());
    }

    /// <summary>Печатная форма договора (HTML).</summary>
    /// <remarks>Самодостаточный HTML для просмотра, печати и сохранения. Токен передаётся заголовком, поэтому фронтенд загружает документ через fetch.</remarks>
    /// <response code="200">HTML-документ.</response>
    /// <response code="403">Нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена или договор ещё не сформирован.</response>
    [HttpGet("document")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(string))]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Document(Guid id, CancellationToken cancellationToken)
    {
        var (application, contract) = await contracts.GetAsync(id, User.ToActor(), cancellationToken);
        var html = ContractDocumentRenderer.Render(application, contract, ContractSchedule.Build(application, contract));
        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>Запросить код подтверждения для подписания договора.</summary>
    /// <remarks>
    /// Роль Client, статус заявки ContractSigning. Код доставляется в ленту событий кабинета (имитация SMS)
    /// и действует 10 минут; повторный запрос выдаёт новый код. В демо-режиме код дублируется в ответе (demoCode).
    /// </remarks>
    /// <response code="200">Код выдан.</response>
    /// <response code="403">Не клиент или нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">Заявка не на подписании или срок подписания истёк.</response>
    [HttpPost("sign/request-code")]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<SignCodeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SignCodeResponse>> RequestCode(Guid id, CancellationToken cancellationToken)
    {
        var issued = await contracts.RequestSigningCodeAsync(id, User.ToActor(), cancellationToken);
        return new SignCodeResponse(issued.ContractNumber, "Feed", issued.CodeExpiresAt, issued.AttemptsLeft, issued.RevealedCode);
    }

    /// <summary>Подписать договор кодом подтверждения.</summary>
    /// <remarks>
    /// Роль Client. Верный код переводит заявку в Signed и ставит в очередь выдачу средств: через несколько секунд статус
    /// станет Disbursed. Неверный код расходует попытку и возвращает 400 с errors.code.
    /// </remarks>
    /// <response code="200">Договор подписан, возвращается актуальное состояние заявки.</response>
    /// <response code="400">Код не передан, не 6 цифр или неверный (errors.code).</response>
    /// <response code="403">Не клиент или нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">Код не запрашивали, его срок истёк, попытки исчерпаны или заявка не на подписании.</response>
    [HttpPost("sign")]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Sign(Guid id, SignContractRequest request, CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var application = await contracts.SignAsync(id, request.Code, actor, cancellationToken);
        return application.ToDto(actor, clock.GetUtcNow());
    }
}
