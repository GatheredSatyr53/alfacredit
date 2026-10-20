using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Features.Applications;
using AlfaCredit.Core.Features.Decisions;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Processing;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.Infrastructure.Persistence;
using AlfaCredit.Infrastructure.Persistence.Repositories;
using AlfaCredit.Infrastructure.Persistence.Seeding;
using AlfaCredit.Infrastructure.Processing;
using AlfaCredit.Infrastructure.Scoring;
using AlfaCredit.Infrastructure.Security;
using AlfaCredit.Infrastructure.Simulators;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is not configured (env: ConnectionStrings__Default).");

        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        services.AddOptions<SeedOptions>().Bind(configuration.GetSection(SeedOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ProcessingOptions>().Bind(configuration.GetSection(ProcessingOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<DecisionPolicyOptions>().Bind(configuration.GetSection(DecisionPolicyOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SimulatorOptions>().Bind(configuration.GetSection(SimulatorOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<ScoringOptions>().Bind(configuration.GetSection(ScoringOptions.SectionName)).ValidateDataAnnotations().ValidateOnStart();

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.EnableRetryOnFailure(maxRetryCount: 3)));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<AppDbContext>());

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICreditProductRepository, CreditProductRepository>();
        services.AddScoped<ICreditApplicationRepository, CreditApplicationRepository>();
        services.AddScoped<IProcessingJobRepository, ProcessingJobRepository>();
        services.AddScoped<IFeedRepository, FeedRepository>();

        services.AddSingleton<IPasswordHasher, IdentityPasswordHasher>();
        services.AddScoped<DbSeeder>();
        services.TryAddSingleton(TimeProvider.System);

        // Симуляторы внешних источников: ЕГРЮЛ, ФНС, БКИ, ФССП
        services.AddSingleton<IEgrulClient, EgrulSimulator>();
        services.AddSingleton<IFnsClient, FnsSimulator>();
        services.AddSingleton<ICreditBureauClient, CreditBureauSimulator>();
        services.AddSingleton<IFsspClient, FsspSimulator>();

        // Скоринг: встроенные правила или внешний сервис с откатом на правила
        services.AddSingleton<RuleBasedScoringService>();
        var scoringMode = configuration.GetValue<ScoringMode?>($"{ScoringOptions.SectionName}:Mode") ?? ScoringMode.Rules;
        if (scoringMode == ScoringMode.External)
        {
            services.AddHttpClient<HttpScoringService>((provider, client) =>
            {
                var scoring = provider.GetRequiredService<IOptions<ScoringOptions>>().Value;
                var baseUrl = string.IsNullOrWhiteSpace(scoring.BaseUrl) ? "http://scoring:8000" : scoring.BaseUrl;
                client.BaseAddress = new Uri(baseUrl.EndsWith('/') ? baseUrl : baseUrl + "/");
                client.Timeout = TimeSpan.FromSeconds(scoring.TimeoutSeconds);
            });
            services.AddScoped<IScoringService>(provider => provider.GetRequiredService<HttpScoringService>());
        }
        else
        {
            services.AddSingleton<IScoringService>(provider => provider.GetRequiredService<RuleBasedScoringService>());
        }

        // Сценарии и конвейер
        services.AddSingleton(provider => new DecisionPolicy(provider.GetRequiredService<IOptions<DecisionPolicyOptions>>().Value));
        services.AddScoped<FeedService>();
        services.AddScoped<ApplicationService>();
        services.AddScoped<ApplicationPipeline>();
        services.AddScoped<ProcessingFailureHandler>();
        services.AddScoped<OfferExpiryService>();
        services.AddHostedService<ProcessingWorker>();

        return services;
    }
}
