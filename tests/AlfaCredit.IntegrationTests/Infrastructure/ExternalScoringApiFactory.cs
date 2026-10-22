using Microsoft.AspNetCore.Hosting;

namespace AlfaCredit.IntegrationTests.Infrastructure;

/// <summary>Стенд с внешним скорингом: API ходит в заглушку сервиса по контракту, таймауты и повторы короткие.</summary>
public sealed class ExternalScoringApiFactory : ApiFactory
{
    public StubScoringServer Scoring { get; } = new();

    protected override Task StartDependenciesAsync()
    {
        Scoring.Start();
        return Task.CompletedTask;
    }

    protected override Task StopDependenciesAsync()
    {
        Scoring.Dispose();
        return Task.CompletedTask;
    }

    protected override void ConfigureTestSettings(IWebHostBuilder builder)
    {
        builder.UseSetting("Scoring:Mode", "External");
        builder.UseSetting("Scoring:BaseUrl", Scoring.BaseUrl);
        builder.UseSetting("Scoring:TimeoutSeconds", "2");
        builder.UseSetting("Scoring:Retries", "1");
        builder.UseSetting("Scoring:RetryDelayMs", "50");
    }
}

[CollectionDefinition(Name)]
public sealed class ExternalScoringCollection : ICollectionFixture<ExternalScoringApiFactory>
{
    public const string Name = "api-external-scoring";
}
