using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AlfaCredit.Api.Setup;

public static class HealthChecksSetup
{
    public const string ReadyTag = "ready";

    public static WebApplication MapApiHealthChecks(this WebApplication app)
    {
        // liveness: процесс жив, без обращения к зависимостям
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
        });

        // readiness: приложение готово принимать трафик, база отвечает
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = registration => registration.Tags.Contains(ReadyTag),
            ResponseWriter = WriteReportAsync,
        });

        return app;
    }

    private static Task WriteReportAsync(HttpContext context, HealthReport report)
    {
        var payload = new HealthReportDto(
            report.Status.ToString(),
            Math.Round(report.TotalDuration.TotalMilliseconds, 1),
            report.Entries
                .Select(entry => new HealthEntryDto(
                    entry.Key,
                    entry.Value.Status.ToString(),
                    Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
                    entry.Value.Exception?.Message))
                .ToArray());

        return context.Response.WriteAsJsonAsync(payload, context.RequestAborted);
    }

    private sealed record HealthReportDto(string Status, double TotalDurationMs, HealthEntryDto[] Checks);

    private sealed record HealthEntryDto(string Name, string Status, double DurationMs, string? Error);
}
