using System.Net;
using System.Text;
using System.Text.Json;
using AlfaCredit.Api.Setup;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.Infrastructure.Scoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

public sealed class ScoringHealthCheckTests
{
    [Theory]
    [InlineData("correct", HealthStatus.Healthy)]
    [InlineData("version", HealthStatus.Unhealthy)]
    [InlineData("hash", HealthStatus.Unhealthy)]
    [InlineData("type", HealthStatus.Unhealthy)]
    [InlineData("array", HealthStatus.Unhealthy)]
    public async Task Readiness_validates_identity_of_the_loaded_model(string variant, HealthStatus expected)
    {
        var options = Options.Create(new ScoringOptions { ModelVersion = LogRegScoringService.ModelVersion });
        var embedded = new EmbeddedScoringService(options);
        var body = variant == "array" ? "[]" : JsonSerializer.Serialize(new
        {
            model_version = variant == "version" ? "rules-0.3" : embedded.ModelVersion,
            artifact_sha256 = variant == "hash" ? "different" : embedded.ArtifactSha256,
        });
        if (variant == "type") body = """{"model_version":{},"artifact_sha256":123}""";
        var check = new ScoringServiceHealthCheck(new Factory(body), options, embedded);
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(expected);
    }

    private sealed class Factory(string body) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new Handler(body));
    }
    private sealed class Handler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
