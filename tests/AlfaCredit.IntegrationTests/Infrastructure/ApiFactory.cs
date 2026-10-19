using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AlfaCredit.IntegrationTests.Infrastructure;

/// <summary>Поднимает API в памяти поверх настоящего PostgreSQL в контейнере, с миграциями и seed-данными.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DemoPassword = "Demo123!";

    private readonly PostgreSqlContainer? _database = DockerAvailability.IsAvailable
        ? new PostgreSqlBuilder("postgres:16.15-alpine").Build()
        : null;

    async Task IAsyncLifetime.InitializeAsync()
    {
        if (_database is not null)
        {
            await _database.StartAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
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
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Name = "api";
}
