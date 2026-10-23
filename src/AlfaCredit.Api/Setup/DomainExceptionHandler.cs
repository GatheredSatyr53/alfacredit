using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Setup;

/// <summary>Переводит исключения бизнес-логики в ProblemDetails: 400 с ошибками по полям, 403, 404, 409.</summary>
internal sealed class DomainExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Ошибка валидации",
                Detail = "Исправьте указанные поля и повторите запрос.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Не найдено",
                Detail = notFound.Message,
            },
            ForbiddenException forbidden => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Доступ запрещён",
                Detail = forbidden.Message,
            },
            InvalidStatusTransitionException transition => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Действие недоступно в текущем статусе",
                Detail = transition.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Действие сейчас невозможно",
                Detail = conflict.Message,
            },
            ConcurrencyConflictException concurrency => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Данные изменились параллельно",
                Detail = concurrency.Message,
            },
            _ => null,
        };

        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
