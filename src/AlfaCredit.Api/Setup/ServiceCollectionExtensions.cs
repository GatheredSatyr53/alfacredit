using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AlfaCredit.Api.Realtime;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AlfaCredit.Api.Setup;

public static class ServiceCollectionExtensions
{
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

        // Ошибки валидации входных данных: ключи полей в camelCase, как в JSON, сообщения по-русски
        services.Configure<ApiBehaviorOptions>(options =>
            options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);

        // Единый формат ошибок RFC 9457 (ProblemDetails) с traceId для поддержки
        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<DomainExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // AllowCredentials нужен SignalR-клиенту в браузере; origin'ы перечислены явно
        services.AddCors(options => options.AddDefaultPolicy(policy => policy
            .WithOrigins(ParseOrigins(configuration["Cors:Origins"]))
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .WithExposedHeaders("Location")));

        services.AddSignalR();
        services.AddSingleton<IApplicationNotifier, SignalRApplicationNotifier>();

        // Защита от перебора паролей: лимит попыток входа на один IP
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        services.AddApiSwagger();

        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: [HealthChecksSetup.ReadyTag]);

        return services;
    }

    private static string[] ParseOrigins(string? origins) =>
        (origins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
