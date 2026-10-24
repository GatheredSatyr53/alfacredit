using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class PipelineEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Stable_client_is_approved_automatically_and_can_accept_the_offer()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 1_500_000, 12, "WorkingCapital");

        var settled = await client.WaitForDecisionAsync(id);

        settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("Approved");
        var decision = settled.GetProperty("decision");
        decision.GetProperty("type").GetString().ShouldBe("Approved");
        decision.GetProperty("isFinal").GetBoolean().ShouldBeTrue();
        decision.GetProperty("reasons").GetArrayLength().ShouldBeGreaterThan(0);
        var offer = settled.GetProperty("offer");
        offer.GetProperty("amount").GetDecimal().ShouldBe(1_500_000);
        offer.GetProperty("interestRate").GetDecimal().ShouldBe(19.5m);
        offer.GetProperty("monthlyPayment").GetDecimal().ShouldBe(138_593.06m);
        offer.GetProperty("totalPayment").GetDecimal().ShouldBe(1_663_116.74m, "итоги по фактическим условиям видны до принятия");
        offer.GetProperty("totalInterest").GetDecimal().ShouldBe(163_116.74m);
        offer.GetProperty("response").GetString().ShouldBe("Pending");
        settled.GetProperty("scoring").ValueKind.ShouldBe(JsonValueKind.Null, "клиент не видит скоринг");
        settled.GetProperty("checks").ValueKind.ShouldBe(JsonValueKind.Null, "клиент не видит сырые проверки");
        settled.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["acceptOffer", "declineOffer"], ignoreOrder: true);
        settled.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("toStatus").GetString()).ShouldBe(
            ["Submitted", "Validating", "Enriching", "Scoring", "Deciding", "Approved"]);

        var accepted = await client.PostAsync($"/api/applications/{id}/offer/accept", content: null);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetProperty("code").GetString().ShouldBe("ContractSigning");
        body.GetProperty("offer").GetProperty("response").GetString().ShouldBe("Accepted");
        body.GetProperty("contract").GetProperty("totalPayment").GetDecimal().ShouldBe(1_663_116.74m, "договор повторяет итоги предложения");
        body.GetProperty("contract").GetProperty("totalInterest").GetDecimal().ShouldBe(163_116.74m);

        (await client.PostAsync($"/api/applications/{id}/offer/accept", content: null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [DockerFact]
    public async Task Limit_products_get_an_offer_without_total_payment_and_overpayment()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();

        foreach (var (productCode, termMonths, rate) in new[] { ("overdraft", 6, 21m), ("credit-line", 12, 18.5m) })
        {
            var id = await client.CreateApplicationAsync(companyId, productCode, 1_000_000, termMonths, "WorkingCapital");
            var settled = await client.WaitForDecisionAsync(id);

            settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("Approved", productCode);
            var offer = settled.GetProperty("offer");
            offer.GetProperty("amount").GetDecimal().ShouldBe(1_000_000, productCode);
            offer.GetProperty("interestRate").GetDecimal().ShouldBe(rate, productCode);
            offer.GetProperty("totalPayment").ValueKind.ShouldBe(JsonValueKind.Null, $"{productCode}: проценты зависят от использования лимита");
            offer.GetProperty("totalInterest").ValueKind.ShouldBe(JsonValueKind.Null, productCode);

            // Принятие работает как раньше, итоги в предложении остаются null
            var accepted = await client.PostAsync($"/api/applications/{id}/offer/accept", content: null);
            accepted.StatusCode.ShouldBe(HttpStatusCode.OK, productCode);
            var body = await accepted.Content.ReadFromJsonAsync<JsonElement>();
            body.GetProperty("status").GetProperty("code").GetString().ShouldBe("ContractSigning", productCode);
            body.GetProperty("offer").GetProperty("totalPayment").ValueKind.ShouldBe(JsonValueKind.Null, productCode);
            body.GetProperty("offer").GetProperty("totalInterest").ValueKind.ShouldBe(JsonValueKind.Null, productCode);
        }
    }

    [DockerFact]
    public async Task Medium_client_gets_conditions_and_can_decline()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_medium");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 3_000_000, 24, "WorkingCapital");

        var settled = await client.WaitForDecisionAsync(id);

        settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("ApprovedWithConditions");
        var decision = settled.GetProperty("decision");
        decision.GetProperty("approvedAmount").GetDecimal().ShouldBe(2_400_000);
        decision.GetProperty("interestRate").GetDecimal().ShouldBe(22.5m);
        decision.GetProperty("conditions").GetString().ShouldNotBeNullOrWhiteSpace();
        decision.GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ShouldContain("amount_reduced");
        var offer = settled.GetProperty("offer");
        offer.GetProperty("amount").GetDecimal().ShouldBe(2_400_000);
        offer.GetProperty("totalPayment").GetDecimal().ShouldBe(2_400_000 + offer.GetProperty("totalInterest").GetDecimal(), "итоги по изменённым условиям, не по запрошенной сумме");
        offer.GetProperty("totalInterest").GetDecimal().ShouldBeInRange(600_000m, 630_000m);

        var declined = await client.PostAsJsonAsync($"/api/applications/{id}/offer/decline", new { reason = "Нужна полная сумма" });
        declined.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await declined.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetProperty("code").GetString().ShouldBe("OfferDeclined");
        body.GetProperty("status").GetProperty("isTerminal").GetBoolean().ShouldBeTrue();
    }

    [DockerFact]
    public async Task Risky_client_is_rejected_with_explained_reasons_and_bank_sees_details()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_bad");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "express", 500_000, 6, "WorkingCapital");

        var settled = await client.WaitForDecisionAsync(id);

        settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("Rejected");
        var reasons = settled.GetProperty("decision").GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ToArray();
        reasons.ShouldContain("tax_debt");
        reasons.ShouldContain("overdue_90");
        settled.GetProperty("offer").ValueKind.ShouldBe(JsonValueKind.Null);

        using var manager = await factory.CreateClient().LoginAsAsync("manager");
        var bankView = await manager.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        bankView.GetProperty("scoring").GetProperty("grade").GetString().ShouldBe("E");
        bankView.GetProperty("scoring").GetProperty("factors").GetArrayLength().ShouldBeGreaterThan(0);
        bankView.GetProperty("checks").GetArrayLength().ShouldBe(4);
        bankView.GetProperty("checks").EnumerateArray().Select(c => c.GetProperty("status").GetString()).ShouldAllBe(s => s == "Succeeded");
    }

    [DockerFact]
    public async Task Large_amount_is_routed_to_manual_review_and_managers_get_feed_item()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_manual");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 8_000_000, 24, "WorkingCapital");

        var settled = await client.WaitForDecisionAsync(id);

        settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("ManualReview");
        var decision = settled.GetProperty("decision");
        decision.GetProperty("type").GetString().ShouldBe("ManualReview");
        decision.GetProperty("isFinal").GetBoolean().ShouldBeFalse();
        decision.GetProperty("recommendedType").GetString().ShouldBe("Approved");
        settled.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["cancel"]);

        using var manager = await factory.CreateClient().LoginAsAsync("manager");
        var feed = await manager.GetFromJsonAsync<JsonElement>("/api/feed?pageSize=50");
        feed.GetProperty("items").EnumerateArray()
            .Where(f => f.GetProperty("applicationId").ValueKind == JsonValueKind.String && f.GetProperty("applicationId").GetGuid() == id)
            .ShouldNotBeEmpty();
    }

    [DockerFact]
    public async Task Feed_tracks_application_events_and_read_state()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "express", 300_000, 6, "Other");
        await client.WaitForDecisionAsync(id);

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/feed?unreadOnly=true&pageSize=50");
        var mine = feed.GetProperty("items").EnumerateArray()
            .Where(f => f.GetProperty("applicationId").ValueKind == JsonValueKind.String && f.GetProperty("applicationId").GetGuid() == id)
            .ToArray();
        mine.Length.ShouldBeGreaterThanOrEqualTo(2, "принята + решение");
        mine.Select(f => f.GetProperty("type").GetString()).ShouldContain("StatusChanged");

        var unreadBefore = (await client.GetFromJsonAsync<JsonElement>("/api/feed/unread-count")).GetProperty("unread").GetInt32();
        unreadBefore.ShouldBeGreaterThanOrEqualTo(mine.Length);

        var first = mine[0].GetProperty("id").GetGuid();
        var marked = await client.PostAsync($"/api/feed/{first}/read", content: null);
        marked.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await marked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("isRead").GetBoolean().ShouldBeTrue();

        var readAll = await client.PostAsync("/api/feed/read-all", content: null);
        readAll.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>("/api/feed/unread-count")).GetProperty("unread").GetInt32().ShouldBe(0);

        using var stranger = await factory.CreateClient().LoginAsAsync("client_bad");
        (await stranger.PostAsync($"/api/feed/{first}/read", content: null)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
