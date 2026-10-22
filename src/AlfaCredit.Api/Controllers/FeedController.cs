using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Common;
using AlfaCredit.Api.Contracts.Feed;
using AlfaCredit.Core.Features.Feed;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>Лента событий пользователя: статусы заявок, решения, предложения, запросы банка.</summary>
[ApiController]
[Route("api/feed")]
[Produces("application/json")]
[Authorize]
public sealed class FeedController(FeedService feed) : ControllerBase
{
    /// <summary>События ленты от новых к старым.</summary>
    /// <param name="unreadOnly">Только непрочитанные.</param>
    /// <param name="page">Номер страницы, с 1.</param>
    /// <param name="pageSize">Размер страницы, 1..100.</param>
    /// <param name="cancellationToken"></param>
    /// <response code="200">Страница событий.</response>
    [HttpGet]
    [ProducesResponseType<PagedResponse<FeedItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResponse<FeedItemDto>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await feed.ListAsync(User.GetUserId(), unreadOnly, page, pageSize, cancellationToken);
        return PagedResponse<FeedItemDto>.From(result, FeedItemDto.From);
    }

    /// <summary>Число непрочитанных событий для бейджа в шапке.</summary>
    /// <response code="200">Счётчик.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> UnreadCount(CancellationToken cancellationToken) =>
        new UnreadCountDto(await feed.CountUnreadAsync(User.GetUserId(), cancellationToken));

    /// <summary>Отметить событие прочитанным.</summary>
    /// <response code="200">Событие обновлено.</response>
    /// <response code="404">Событие не найдено или принадлежит другому пользователю.</response>
    [HttpPost("{id:guid}/read")]
    [ProducesResponseType<FeedItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FeedItemDto>> MarkRead(Guid id, [FromServices] Core.Abstractions.IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var item = await feed.MarkReadAsync(id, User.GetUserId(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return FeedItemDto.From(item);
    }

    /// <summary>Отметить все события прочитанными.</summary>
    /// <response code="200">Сколько событий отмечено.</response>
    [HttpPost("read-all")]
    [ProducesResponseType<UnreadCountDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountDto>> MarkAllRead([FromServices] Core.Abstractions.IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        var count = await feed.MarkAllReadAsync(User.GetUserId(), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new UnreadCountDto(count);
    }
}
