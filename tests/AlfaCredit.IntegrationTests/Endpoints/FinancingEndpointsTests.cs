using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

/// <summary>POST /api/financing/preview и поле needByDate заявки: контракт в docs/financing-preview.md.</summary>
[Collection(ApiCollection.Name)]
public sealed class FinancingEndpointsTests(ApiFactory factory)
{
    private static string Today(int plusDays) => DateOnly.FromDateTime(DateTime.UtcNow).AddDays(plusDays).ToString("yyyy-MM-dd");

    [DockerFact]
    public async Task Preview_returns_options_and_apply_with_creates_an_application_with_need_by_date()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();
        var before = (await client.GetFromJsonAsync<JsonElement>("/api/applications?pageSize=1")).GetProperty("totalCount").GetInt32();
        var needByDate = Today(30);

        var response = await client.PostAsJsonAsync("/api/financing/preview", new { companyId, amount = 1_500_000, purpose = "WorkingCapital", needByDate, termMonths = 12 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>();
        preview.GetProperty("company").GetProperty("id").GetGuid().ShouldBe(companyId);
        preview.GetProperty("request").GetProperty("needByDate").GetString().ShouldBe(needByDate);
        preview.GetProperty("request").GetProperty("daysUntilNeeded").GetInt32().ShouldBe(30);
        preview.GetProperty("request").GetProperty("purposeTitle").GetString().ShouldBe("Пополнение оборотных средств");
        preview.GetProperty("summary").GetString()!.ShouldStartWith("Подходят 4 варианта из 5.");
        preview.GetProperty("disclaimer").GetString()!.ShouldContain("Заявка не создана");

        var options = preview.GetProperty("options").EnumerateArray().ToList();
        options.Select(o => o.GetProperty("productCode").GetString()).ShouldBe(["credit-line", "working-capital", "overdraft", "express"]);
        var best = options[0];
        best.GetProperty("isRecommended").GetBoolean().ShouldBeTrue();
        best.GetProperty("termSuggested").GetBoolean().ShouldBeFalse();
        best.GetProperty("cost").GetProperty("monthlyPayment").GetDecimal().ShouldBe(137_877.18m);
        best.GetProperty("cost").GetProperty("totalInterest").GetDecimal().ShouldBe(154_526.12m);
        best.GetProperty("cost").GetProperty("maxInterestRate").GetDecimal().ShouldBe(21.5m);
        best.GetProperty("affordability").GetProperty("level").GetString().ShouldBe("Moderate");
        best.GetProperty("affordability").GetProperty("debtLoad").GetDecimal().ShouldBe(0.48m);
        best.GetProperty("funding").GetProperty("decisionPath").GetString().ShouldBe("Auto");
        best.GetProperty("funding").GetProperty("inTime").GetBoolean().ShouldBeTrue();
        best.GetProperty("explanations").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ShouldContain("cheapest");
        best.GetProperty("limitations").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ShouldBe(["rate_depends_on_risk"]);
        best.GetProperty("requirements").EnumerateArray().Select(e => e.GetProperty("code").GetString()).ShouldBe(["credit_history_consent"]);
        options[1].GetProperty("cost").GetProperty("monthlyPayment").GetDecimal().ShouldBe(138_593.06m);
        preview.GetProperty("unsuitable").EnumerateArray().Single().GetProperty("productCode").GetString().ShouldBe("investment");

        // Preview ничего не создал
        (await client.GetFromJsonAsync<JsonElement>("/api/applications?pageSize=1")).GetProperty("totalCount").GetInt32().ShouldBe(before);

        // Тело из applyWith уходит в POST /api/applications без изменений
        var applyWith = best.GetProperty("applyWith");
        applyWith.GetProperty("companyId").GetGuid().ShouldBe(companyId);
        applyWith.GetProperty("needByDate").GetString().ShouldBe(needByDate);
        var created = await client.PostAsync("/api/applications", new StringContent(applyWith.GetRawText(), System.Text.Encoding.UTF8, "application/json"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var application = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        application.GetProperty("needByDate").GetString().ShouldBe(needByDate);
        application.GetProperty("product").GetProperty("code").GetString().ShouldBe("credit-line");
        application.GetProperty("termMonths").GetInt32().ShouldBe(12);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/applications?pageSize=50");
        list.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == id).GetProperty("needByDate").GetString().ShouldBe(needByDate);
    }

    [DockerFact]
    public async Task Application_without_need_by_date_still_works_and_reads_back_null()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        var created = await client.PostAsJsonAsync("/api/applications", new { companyId, productCode = "working-capital", amount = 1_000_000, termMonths = 12, purpose = "WorkingCapital" });

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var application = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        application.GetProperty("needByDate").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [DockerFact]
    public async Task Term_is_suggested_when_not_given_and_young_business_gets_reasons()
    {
        using var good = await factory.CreateClient().LoginAsAsync("client_good");
        var goodCompany = await good.GetFirstCompanyIdAsync();

        var suggested = await good.PostAsJsonAsync("/api/financing/preview", new { companyId = goodCompany, amount = 1_500_000, purpose = "WorkingCapital", needByDate = Today(30) });
        suggested.StatusCode.ShouldBe(HttpStatusCode.OK);
        var options = (await suggested.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("options").EnumerateArray().ToList();
        options.ShouldAllBe(o => o.GetProperty("termSuggested").GetBoolean() && o.GetProperty("termMonths").GetInt32() >= 11 && o.GetProperty("termMonths").GetInt32() <= 12);
        options.ShouldAllBe(o => o.GetProperty("affordability").GetProperty("level").GetString() == "Moderate");

        using var young = await factory.CreateClient().LoginAsAsync("client_new");
        var youngCompany = await young.GetFirstCompanyIdAsync();

        var none = await young.PostAsJsonAsync("/api/financing/preview", new { companyId = youngCompany, amount = 500_000, purpose = "WorkingCapital", needByDate = Today(40) });
        none.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await none.Content.ReadFromJsonAsync<JsonElement>();
        preview.GetProperty("options").GetArrayLength().ShouldBe(0);
        preview.GetProperty("unsuitable").GetArrayLength().ShouldBe(5);
        preview.GetProperty("summary").GetString()!.ShouldStartWith("Подходящих продуктов нет.");
        preview.GetProperty("unsuitable").EnumerateArray()
            .ShouldAllBe(u => u.GetProperty("reasons").EnumerateArray().Any(r => r.GetProperty("code").GetString() == "business_too_young"));
    }

    [DockerFact]
    public async Task Invalid_dates_return_400_with_field_errors_for_preview_and_application()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        var past = await client.PostAsJsonAsync("/api/financing/preview", new { companyId, amount = 1_500_000, purpose = "WorkingCapital", needByDate = Today(-1) });
        past.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await past.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("needByDate")[0].GetString()!.ShouldContain("раньше сегодняшнего дня");

        var malformed = await client.PostAsJsonAsync("/api/financing/preview", new { companyId, amount = 1_500_000, purpose = "WorkingCapital", needByDate = "15.11.2026" });
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await malformed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("needByDate", out _).ShouldBeTrue();

        var missing = await client.PostAsJsonAsync("/api/financing/preview", new { companyId, amount = 0, purpose = "WorkingCapital" });
        missing.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        errors.TryGetProperty("needByDate", out _).ShouldBeTrue();
        errors.TryGetProperty("amount", out _).ShouldBeTrue();

        var application = await client.PostAsJsonAsync("/api/applications", new { companyId, productCode = "working-capital", amount = 1_000_000, termMonths = 12, purpose = "WorkingCapital", needByDate = Today(-1) });
        application.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await application.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("needByDate", out _).ShouldBeTrue();
    }

    [DockerFact]
    public async Task Other_clients_get_403_managers_can_preview_and_anonymous_gets_401()
    {
        using var owner = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await owner.GetFirstCompanyIdAsync();
        var body = new { companyId, amount = 1_500_000, purpose = "WorkingCapital", needByDate = Today(30), termMonths = 12 };

        using var other = await factory.CreateClient().LoginAsAsync("client_medium");
        (await other.PostAsJsonAsync("/api/financing/preview", body)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        using var manager = await factory.CreateClient().LoginAsAsync("manager");
        var response = await manager.PostAsJsonAsync("/api/financing/preview", body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("options").GetArrayLength().ShouldBe(4);

        using var anonymous = factory.CreateClient();
        (await anonymous.PostAsJsonAsync("/api/financing/preview", body)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var unknownCompany = await owner.PostAsJsonAsync("/api/financing/preview", body with { companyId = Guid.NewGuid() });
        unknownCompany.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await unknownCompany.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("companyId", out _).ShouldBeTrue();
    }
}
