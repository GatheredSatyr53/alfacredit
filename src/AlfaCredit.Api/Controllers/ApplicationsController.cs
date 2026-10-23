using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Applications;
using AlfaCredit.Api.Contracts.Common;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Features.Applications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AlfaCredit.Api.Controllers;

/// <summary>Кредитные заявки: подача, просмотр, список, отмена.</summary>
[ApiController]
[Route("api/applications")]
[Produces("application/json")]
[Authorize]
public sealed class ApplicationsController(ApplicationService applications) : ControllerBase
{
    /// <summary>Справочник статусов заявки с текстами для интерфейса.</summary>
    /// <response code="200">Все статусы в порядке прохождения.</response>
    [HttpGet("statuses")]
    [AllowAnonymous]
    [ProducesResponseType<IReadOnlyList<ApplicationStatusDto>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ApplicationStatusDto>> Statuses() =>
        ApplicationStatusCatalog.All.Select(ApplicationStatusDto.From).ToArray();

    /// <summary>Список заявок.</summary>
    /// <remarks>Клиент видит заявки своих компаний, сотрудник банка все заявки. Сортировка от новых к старым.</remarks>
    /// <param name="status">Фильтр по статусу.</param>
    /// <param name="page">Номер страницы, с 1.</param>
    /// <param name="pageSize">Размер страницы, 1..100.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Страница заявок.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<ApplicationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<ApplicationSummaryDto>>> List(
        [FromQuery] ApplicationStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await applications.ListAsync(new ApplicationListQuery(status, page, pageSize), User.ToActor(), cancellationToken);
        return PagedResponse<ApplicationSummaryDto>.From(result, a => a.ToSummaryDto());
    }

    /// <summary>Подать кредитную заявку.</summary>
    /// <remarks>
    /// Проверяет поля запроса и соответствие продукту: сумму, срок, цель и возраст бизнеса.
    /// При ошибке возвращает 400 с перечнем проблем по полям (errors.amount, errors.termMonths и т.д.).
    /// Необязательное поле needByDate (к какой дате нужны деньги) сохраняется и показывается в заявке; тело можно взять из options[].applyWith ответа POST /api/financing/preview.
    /// Заявка создаётся сразу в статусе Submitted и попадает в обработку банка.
    /// </remarks>
    /// <response code="201">Заявка создана. В заголовке Location адрес заявки.</response>
    /// <response code="400">Ошибка валидации с указанием полей.</response>
    /// <response code="403">Пользователь не представитель компании или не клиент.</response>
    [HttpPost]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<CreateApplicationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CreateApplicationResponse>> Create(CreateApplicationRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateApplicationCommand(
            request.CompanyId!.Value,
            request.ProductCode,
            request.Amount,
            request.TermMonths,
            request.Purpose!.Value,
            request.PurposeDescription,
            request.NeedByDate);

        var application = await applications.CreateAsync(command, User.ToActor(), cancellationToken);
        var response = new CreateApplicationResponse(application.Id, application.Number, application.Status);

        return CreatedAtAction(nameof(GetById), new { id = application.Id }, response);
    }

    /// <summary>Заявка по идентификатору: параметры, статус, доступные действия и история.</summary>
    /// <response code="200">Заявка найдена.</response>
    /// <response code="403">Нет доступа к заявке другой компании.</response>
    /// <response code="404">Заявка не найдена.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var application = await applications.GetAsync(id, actor, cancellationToken);
        return application.ToDto(actor);
    }

    /// <summary>Принять предложение банка по одобренной заявке.</summary>
    /// <remarks>Доступно в статусах Approved и ApprovedWithConditions, пока предложение действует. Переводит заявку к подписанию договора и формирует договор (поле contract в ответе).</remarks>
    /// <response code="200">Предложение принято, возвращается актуальное состояние заявки.</response>
    /// <response code="403">Нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">Предложения нет, оно уже закрыто или срок истёк.</response>
    [HttpPost("{id:guid}/offer/accept")]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> AcceptOffer(Guid id, CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var application = await applications.AcceptOfferAsync(id, actor, cancellationToken);
        return application.ToDto(actor);
    }

    /// <summary>Отклонить предложение банка.</summary>
    /// <remarks>Тело запроса необязательно. Заявка переходит в статус OfferDeclined.</remarks>
    /// <response code="200">Предложение отклонено.</response>
    /// <response code="403">Нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">Предложения нет, оно уже закрыто или срок истёк.</response>
    [HttpPost("{id:guid}/offer/decline")]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> DeclineOffer(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] DeclineOfferRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var application = await applications.DeclineOfferAsync(id, actor, request?.Reason, cancellationToken);
        return application.ToDto(actor);
    }

    /// <summary>Отменить заявку.</summary>
    /// <remarks>Доступно клиенту, пока банк не принял решение, а также на этапе подписания договора (отказ от подписания). Тело запроса необязательно.</remarks>
    /// <response code="200">Заявка отменена, возвращается её актуальное состояние.</response>
    /// <response code="403">Нет доступа к заявке.</response>
    /// <response code="404">Заявка не найдена.</response>
    /// <response code="409">В текущем статусе отмена недоступна.</response>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Policy = Policies.ClientOnly)]
    [ProducesResponseType<ApplicationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationDto>> Cancel(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CancelApplicationRequest? request,
        CancellationToken cancellationToken)
    {
        var actor = User.ToActor();
        var application = await applications.CancelAsync(id, actor, request?.Reason, cancellationToken);
        return application.ToDto(actor);
    }
}
