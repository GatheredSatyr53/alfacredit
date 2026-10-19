using System.Net.Sockets;
using AlfaCredit.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace AlfaCredit.Infrastructure.Persistence;

/// <summary>Миграции и seed при старте с повторными попытками: база в compose может подняться чуть позже приложения.</summary>
public static class DatabaseInitializer
{
    private const int MaxAttempts = 10;

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        if (options.MigrateOnStartup)
        {
            var db = services.GetRequiredService<AppDbContext>();
            await RetryAsync(logger, "apply migrations", () => db.Database.MigrateAsync(cancellationToken), cancellationToken);
            logger.LogInformation("Database migrations are up to date");
        }

        var seeder = services.GetRequiredService<DbSeeder>();
        await RetryAsync(logger, "seed demo data", () => seeder.SeedAsync(cancellationToken), cancellationToken);
    }

    private static async Task RetryAsync(ILogger logger, string operation, Func<Task> action, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                var delay = TimeSpan.FromSeconds(Math.Min(2 * attempt, 10));
                logger.LogWarning(ex, "Attempt {Attempt}/{MaxAttempts} to {Operation} failed, retrying in {DelaySeconds}s", attempt, MaxAttempts, operation, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private static bool IsTransient(Exception exception) =>
        exception is NpgsqlException or SocketException or TimeoutException
        || exception.InnerException is NpgsqlException or SocketException or TimeoutException;
}
