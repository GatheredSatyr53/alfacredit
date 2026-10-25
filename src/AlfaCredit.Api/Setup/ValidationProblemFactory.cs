using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AlfaCredit.Api.Setup;

/// <summary>Ответ 400 для ошибок привязки и DataAnnotations: тот же формат, что у доменной валидации.</summary>
internal static class ValidationProblemFactory
{
    private const string BodyKey = "request";
    private const string GenericFieldMessage = "Некорректное значение или формат поля.";
    private const string GenericBodyMessage = "Тело запроса отсутствует или не является корректным JSON.";

    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var (key, entry) in context.ModelState)
        {
            if (entry is null || entry.Errors.Count == 0)
            {
                continue;
            }

            var field = NormalizeKey(key);
            var messages = entry.Errors
                .Select(error => ToMessage(field, error))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            errors[field] = errors.TryGetValue(field, out var existing)
                ? existing.Concat(messages).Distinct(StringComparer.Ordinal).ToArray()
                : messages;
        }

        // Ошибка "тело запроса обязательно" дублирует ошибки конкретных полей, когда они есть
        if (errors.Count > 1)
        {
            errors.Remove(BodyKey);
        }

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Ошибка валидации",
            Detail = "Исправьте указанные поля и повторите запрос.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
        };
        problem.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    private static string ToMessage(string field, ModelError error)
    {
        if (field == BodyKey)
        {
            return GenericBodyMessage;
        }

        // Сообщения самого фреймворка ("The JSON value…", "The value 'abc' is not valid for Amount.") английские: заменяем общим
        return error.Exception is not null
            || string.IsNullOrWhiteSpace(error.ErrorMessage)
            || error.ErrorMessage.StartsWith("The ", StringComparison.Ordinal)
                ? GenericFieldMessage
                : error.ErrorMessage;
    }

    /// <summary>"$.amount" и "Amount" превращаются в "amount"; пустой ключ или имя параметра тела означают запрос целиком.</summary>
    private static string NormalizeKey(string key)
    {
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key.TrimStart('$');
        if (trimmed.Length == 0 || trimmed.Equals(BodyKey, StringComparison.OrdinalIgnoreCase))
        {
            return BodyKey;
        }

        return string.Join('.', trimmed.Split('.').Select(part =>
            part.Length == 0 ? part : char.ToLowerInvariant(part[0]) + part[1..]));
    }
}
