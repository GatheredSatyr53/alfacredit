using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Auth;
using AlfaCredit.Api.Setup;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Features.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlfaCredit.Api.Controllers;

/// <summary>Аутентификация: вход по логину и паролю, текущий пользователь.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public sealed class AuthController(AuthService authService, IUserRepository users) : ControllerBase
{
    /// <summary>Вход по логину и паролю.</summary>
    /// <remarks>Возвращает JWT. Демо-учётки и пароль перечислены в README.</remarks>
    /// <response code="200">Токен выдан.</response>
    /// <response code="400">Запрос не прошёл валидацию.</response>
    /// <response code="401">Неверный логин или пароль, либо учётка отключена.</response>
    /// <response code="429">Слишком много попыток входа с этого адреса.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(ServiceCollectionExtensions.AuthRateLimitPolicy)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request.Login, request.Password, cancellationToken);
        if (!result.IsSuccess)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Неверный логин или пароль",
                detail: "Проверьте введённые данные. Учётная запись также может быть отключена.");
        }

        return new LoginResponse(result.Token.Value, result.Token.ExpiresAt, UserDto.From(result.User));
    }

    /// <summary>Профиль текущего пользователя по токену.</summary>
    /// <response code="200">Профиль найден.</response>
    /// <response code="404">Пользователь из токена больше не существует.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(User.GetUserId(), cancellationToken);
        return user is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, title: "Пользователь не найден")
            : UserDto.From(user);
    }
}
