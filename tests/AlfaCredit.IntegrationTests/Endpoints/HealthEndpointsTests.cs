using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Live_probe_returns_healthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [DockerFact]
    public async Task Ready_probe_reports_database_check()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<JsonElement>();
        report.GetProperty("status").GetString().ShouldBe("Healthy");
        report.GetProperty("checks").EnumerateArray()
            .Select(check => check.GetProperty("name").GetString())
            .ShouldContain("database");
    }

    [DockerFact]
    public async Task Swagger_document_is_served()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        document.GetProperty("paths").TryGetProperty("/api/auth/login", out _).ShouldBeTrue();
    }
}
