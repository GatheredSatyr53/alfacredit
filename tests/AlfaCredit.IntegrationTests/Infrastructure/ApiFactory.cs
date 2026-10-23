using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AlfaCredit.IntegrationTests.Infrastructure;

/// <summary>Поднимает API в памяти поверх настоящего PostgreSQL в контейнере, с миграциями и seed-данными.</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo123!";

    // Program.cs создаёт bootstrap-логгер Serilog в статическом Log.Logger и замораживает его при сборке хоста:
    // сборка двух хостов одновременно в одном процессе падает с «The logger is already frozen», поэтому сборка сериализована.
    private static readonly object HostBuildLock = new();

    private readonly PostgreSqlContainer? _database = DockerAvailability.IsAvailable
        ? new PostgreSqlBuilder("postgres:16.15-alpine").Build()
        : null;

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (_database is not null)
        {
            await _database.StartAsync();
        }

        await StartDependenciesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await StopDependenciesAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    /// <summary>Дополнительные зависимости стенда, например заглушка сервиса скоринга.</summary>
    protected virtual Task StartDependenciesAsync() => Task.CompletedTask;

    protected virtual Task StopDependenciesAsync() => Task.CompletedTask;

    /// <summary>Настройки, которыми отдельный стенд дополняет или переопределяет базовые.</summary>
    protected virtual void ConfigureTestSettings(IWebHostBuilder builder)
    {
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (HostBuildLock)
        {
            return base.CreateHost(builder);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(Environments.Development);
        builder.UseSetting("ConnectionStrings:Default", _database?.GetConnectionString() ?? "Host=localhost;Database=unavailable");
        builder.UseSetting("Jwt:Key", "integration-tests-jwt-signing-key-0123456789abcdef0123456789");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Seed:Enabled", "true");
        builder.UseSetting("Seed:DemoPassword", DemoPassword);
        builder.UseSetting("Swagger:Enabled", "true");
        // Тесты логинятся десятки раз в минуту с одного адреса: боевой лимит 20 попыток здесь мешает
        builder.UseSetting("RateLimiting:LoginPermitLimit", "1000");
        // Конвейер в тестах работает быстро, но с секундной паузой: отмена заявки сразу после создания остаётся возможной
        builder.UseSetting("Processing:StageDelaySeconds", "1");
        builder.UseSetting("Processing:PollIntervalMs", "100");
        builder.UseSetting("Simulators:LatencyMs", "0");
        builder.UseSetting("Simulators:FailureRate", "0");
        ConfigureTestSettings(builder);
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
