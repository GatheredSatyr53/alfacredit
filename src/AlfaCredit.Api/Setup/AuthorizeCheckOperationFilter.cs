using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace AlfaCredit.Api.Setup;

/// <summary>Помечает в Swagger только защищённые методы: замок и ответ 401 появляются там, где есть [Authorize].</summary>
internal sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var allowAnonymous = context.MethodInfo.GetCustomAttributes(true).OfType<AllowAnonymousAttribute>().Any();
        if (allowAnonymous)
        {
            return;
        }

        var declaringTypeAttributes = context.MethodInfo.DeclaringType?.GetCustomAttributes(true) ?? [];
        var hasAuthorize = declaringTypeAttributes.OfType<AuthorizeAttribute>().Any()
            || context.MethodInfo.GetCustomAttributes(true).OfType<AuthorizeAttribute>().Any();
        if (!hasAuthorize)
        {
            return;
        }

        operation.Responses ??= [];
        operation.Responses.TryAdd("401", new OpenApiResponse { Description = "Требуется аутентификация" });
        operation.Responses.TryAdd("403", new OpenApiResponse { Description = "Недостаточно прав" });

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SwaggerSetup.BearerSchemeId, context.Document)] = [],
        });
    }
}
