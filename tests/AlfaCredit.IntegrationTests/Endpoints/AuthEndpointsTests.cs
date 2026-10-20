using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class AuthEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Login_with_seeded_account_returns_token_and_me_returns_profile()
    {
        using var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { login = "manager", password = ApiFactory.DemoPassword });

        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var login = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = login.GetProperty("accessToken").GetString();
        token.ShouldNotBeNullOrWhiteSpace();
        login.GetProperty("user").GetProperty("role").GetString().ShouldBe("Manager");

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var meResponse = await client.GetAsync("/api/auth/me");

        meResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await meResponse.Content.ReadFromJsonAsync<JsonElement>();
        me.GetProperty("login").GetString().ShouldBe("manager");
        me.GetProperty("fullName").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [DockerFact]
    public async Task Login_with_wrong_password_returns_401_problem_details()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "client_good", password = "wrong-password" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("status").GetInt32().ShouldBe(401);
        problem.GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Login_with_empty_body_fields_returns_400_validation_problem()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { login = "", password = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.TryGetProperty("errors", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Me_without_token_returns_401()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [DockerFact]
    public async Task Me_with_tampered_token_returns_401()
    {
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJ4In0.invalid");

        var response = await client.GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
