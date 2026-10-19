using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Setup;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Features.Auth;
using AlfaCredit.Infrastructure;
using AlfaCredit.Infrastructure.Persistence;
using Serilog;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "AlfaCredit.Api");

        if (context.HostingEnvironment.IsDevelopment())
        {
            configuration.WriteTo.Console();
        }
        else
        {
            // Структурные JSON-логи в stdout: их собирает Docker / платформа логирования
            configuration.WriteTo.Console(new CompactJsonFormatter());
        }
    });

    var connectionString = builder.Configuration.GetConnectionString("Default")
        ?? throw new InvalidOperationException("Connection string 'Default' is not configured (env: ConnectionStrings__Default).");

    builder.Services.AddApiServices(builder.Configuration);
    builder.Services.AddInfrastructure(connectionString);
    builder.Services.AddApiAuthentication(builder.Configuration);

    builder.Services.AddScoped<AuthService>();
    builder.Services.AddSingleton<ITokenService, JwtTokenService>();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (app.Configuration.GetValue<bool>("Swagger:Enabled"))
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "AlfaCredit API v1");
            options.DocumentTitle = "AlfaCredit API";
        });
        app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
    }

    app.UseCors();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapApiHealthChecks();

    await app.Services.InitializeDatabaseAsync();
    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "AlfaCredit.Api terminated unexpectedly");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

/// <summary>Точка входа видна интеграционным тестам через WebApplicationFactory.</summary>
public partial class Program;
