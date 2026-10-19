using Microsoft.OpenApi;

namespace AlfaCredit.Api.Setup;

public static class SwaggerSetup
{
    public const string BearerSchemeId = "Bearer";

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "AlfaCredit API",
                Version = "v1",
                Description = "Цифровой кредитный конвейер для малого и микробизнеса: личный кабинет клиента, "
                    + "обработка заявок на стороне банка, скоринг и кредитное решение. "
                    + "Для защищённых методов получите токен через POST /api/v1/auth/login и нажмите Authorize.",
            });

            var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }

            options.SupportNonNullableReferenceTypes();

            options.AddSecurityDefinition(BearerSchemeId, new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "JWT из ответа POST /api/v1/auth/login. Префикс Bearer подставляется автоматически.",
            });

            options.OperationFilter<AuthorizeCheckOperationFilter>();
        });

        return services;
    }
}
