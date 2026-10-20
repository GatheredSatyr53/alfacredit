using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class ApplicationsEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Client_creates_application_reads_it_back_and_sees_it_in_the_list()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        var created = await client.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "working-capital",
            amount = 1_500_000,
            termMonths = 12,
            purpose = "WorkingCapital",
            purposeDescription = "Закупка товара к сезону",
        });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        var id = body.GetProperty("id").GetGuid();
        body.GetProperty("number").GetString()!.ShouldStartWith("AC-");
        body.GetProperty("status").GetString().ShouldBe("Submitted");
        created.Headers.Location.ShouldNotBeNull();
        created.Headers.Location.ToString().ShouldEndWith($"/api/applications/{id}");

        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        fetched.GetProperty("number").GetString().ShouldBe(body.GetProperty("number").GetString());
        fetched.GetProperty("status").GetProperty("code").GetString().ShouldBe("Submitted");
        fetched.GetProperty("status").GetProperty("title").GetString().ShouldBe("Отправлена");
        fetched.GetProperty("status").GetProperty("clientNextAction").GetString().ShouldNotBeNullOrWhiteSpace();
        fetched.GetProperty("amount").GetDecimal().ShouldBe(1_500_000);
        fetched.GetProperty("purposeTitle").GetString().ShouldBe("Пополнение оборотных средств");
        fetched.GetProperty("company").GetProperty("id").GetGuid().ShouldBe(companyId);
        fetched.GetProperty("product").GetProperty("code").GetString().ShouldBe("working-capital");
        fetched.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ShouldContain("cancel");
        fetched.GetProperty("history").GetArrayLength().ShouldBe(1);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/applications?status=Submitted&pageSize=50");
        list.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ShouldContain(id);
        list.GetProperty("totalCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }

    [DockerFact]
    public async Task Invalid_request_returns_400_with_field_errors_in_camel_case()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");

        var response = await client.PostAsJsonAsync("/api/applications", new { productCode = "", amount = 0, termMonths = 0 });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("title").GetString().ShouldBe("Ошибка валидации");
        var errors = problem.GetProperty("errors");
        errors.TryGetProperty("companyId", out _).ShouldBeTrue();
        errors.TryGetProperty("productCode", out _).ShouldBeTrue();
        errors.TryGetProperty("amount", out _).ShouldBeTrue();
        errors.TryGetProperty("termMonths", out _).ShouldBeTrue();
        errors.TryGetProperty("purpose", out _).ShouldBeTrue();
        errors.GetProperty("amount")[0].GetString()!.ShouldContain("Сумма");
        problem.TryGetProperty("traceId", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Product_mismatch_returns_400_with_domain_errors_by_field()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        var response = await client.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "overdraft",
            amount = 50_000_000,
            termMonths = 60,
            purpose = "Equipment",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("amount", out var amount).ShouldBeTrue();
        amount[0].GetString()!.ShouldContain("Овердрафт");
        errors.TryGetProperty("termMonths", out _).ShouldBeTrue();
        errors.TryGetProperty("purpose", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Unknown_enum_value_returns_400_not_500()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        var response = await client.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "express",
            amount = 500_000,
            termMonths = 6,
            purpose = "Vacation",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("purpose", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Young_business_gets_explanation_instead_of_a_loan()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_new");
        var companyId = await client.GetFirstCompanyIdAsync();

        var response = await client.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "express",
            amount = 500_000,
            termMonths = 6,
            purpose = "WorkingCapital",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.GetProperty("companyId")[0].GetString()!.ShouldContain("Возраст бизнеса");
    }

    [DockerFact]
    public async Task Other_clients_get_403_managers_can_read_and_unknown_id_is_404()
    {
        using var owner = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await owner.GetFirstCompanyIdAsync();
        var created = await owner.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "credit-line",
            amount = 2_000_000,
            termMonths = 24,
            purpose = "Expansion",
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        using var stranger = await factory.CreateClient().LoginAsAsync("client_bad");
        using var manager = await factory.CreateClient().LoginAsAsync("manager");

        (await stranger.GetAsync($"/api/applications/{id}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await manager.GetAsync($"/api/applications/{id}")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await owner.GetAsync($"/api/applications/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var foreignCompany = await stranger.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "express",
            amount = 500_000,
            termMonths = 6,
            purpose = "Other",
        });
        foreignCompany.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var managerCreate = await manager.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "express",
            amount = 500_000,
            termMonths = 6,
            purpose = "Other",
        });
        managerCreate.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [DockerFact]
    public async Task Client_can_cancel_submitted_application_once()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_medium");
        var companyId = await client.GetFirstCompanyIdAsync();
        var created = await client.PostAsJsonAsync("/api/applications", new
        {
            companyId,
            productCode = "express",
            amount = 800_000,
            termMonths = 12,
            purpose = "Equipment",
        });
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var cancelled = await client.PostAsJsonAsync($"/api/applications/{id}/cancel", new { reason = "Нашли другой источник" });
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetProperty("code").GetString().ShouldBe("Cancelled");
        body.GetProperty("history").GetArrayLength().ShouldBe(2);
        body.GetProperty("history")[1].GetProperty("comment").GetString().ShouldBe("Нашли другой источник");

        var again = await client.PostAsync($"/api/applications/{id}/cancel", content: null);
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()!.ShouldContain("Отменена");
    }

    [DockerFact]
    public async Task Statuses_catalog_is_public_and_complete()
    {
        using var client = factory.CreateClient();

        var statuses = await client.GetFromJsonAsync<JsonElement>("/api/applications/statuses");

        var codes = statuses.EnumerateArray().Select(s => s.GetProperty("code").GetString()).ToArray();
        codes.ShouldContain("Submitted");
        codes.ShouldContain("Disbursed");
        codes.Length.ShouldBe(16);
    }
}
