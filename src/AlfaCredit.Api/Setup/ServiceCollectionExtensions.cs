using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AlfaCredit.Api.Realtime;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Infrastructure.Persistence;
using AlfaCredit.Infrastructure.Scoring;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

        // Защита от перебора паролей: лимит попыток входа на один IP (RateLimiting:LoginPermitLimit за LoginWindowSeconds)
        var loginPermitLimit = Math.Max(1, configuration.GetValue<int?>("RateLimiting:LoginPermitLimit") ?? 20);
        var loginWindow = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue<int?>("RateLimiting:LoginWindowSeconds") ?? 60));
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = loginPermitLimit,
                        Window = loginWindow,
                        QueueLimit = 0,
                    }));
        });

        services.AddApiSwagger();

        var healthChecks = services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database", tags: [HealthChecksSetup.ReadyTag]);

        // Внешний скоринг: недоступность не делает API неготовым, но видна в /health/ready
        if (configuration.GetValue<ScoringMode?>($"{ScoringOptions.SectionName}:Mode") == ScoringMode.External)
        {
            services.AddHttpClient(ScoringServiceHealthCheck.ClientName, client => client.Timeout = TimeSpan.FromSeconds(3));
            healthChecks.AddCheck<ScoringServiceHealthCheck>("scoring", failureStatus: HealthStatus.Degraded, tags: [HealthChecksSetup.ReadyTag]);
        }

        return services;
    }

    private static string[] ParseOrigins(string? origins) =>
        (origins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
