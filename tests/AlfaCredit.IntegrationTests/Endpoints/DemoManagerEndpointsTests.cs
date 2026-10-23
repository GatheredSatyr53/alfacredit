using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

/// <summary>Демо-режим менеджера: заявка из ручного рассмотрения получает смоделированное решение, клиент продолжает оформление.</summary>
[Collection(ApiCollection.Name)]
public sealed class DemoManagerEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Manual_review_gets_a_simulated_manager_approval_and_the_client_can_accept_the_offer()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_manual");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 8_000_000, 24, "WorkingCapital");

        var manual = await client.WaitForDecisionAsync(id);
        manual.GetProperty("status").GetProperty("code").GetString().ShouldBe("ManualReview");
        manual.GetProperty("decision").GetProperty("isSimulated").GetBoolean().ShouldBeFalse();

        // Стенд настроен на одобрение с задержкой в несколько секунд
        var decided = await client.WaitForStatusAsync(id, "ApprovedWithConditions", TimeSpan.FromSeconds(40));
        var decision = decided.GetProperty("decision");
        decision.GetProperty("decidedBy").GetString().ShouldBe("Manager");
        decision.GetProperty("isSimulated").GetBoolean().ShouldBeTrue();
        decision.GetProperty("isFinal").GetBoolean().ShouldBeTrue();
        decision.GetProperty("approvedAmount").GetDecimal().ShouldBe(8_000_000);
        decision.GetProperty("interestRate").GetDecimal().ShouldBe(21m);
        decision.GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString()).ShouldContain("demo_manager");
        decided.GetProperty("offer").GetProperty("amount").GetDecimal().ShouldBe(8_000_000);
        decided.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["acceptOffer", "declineOffer"], ignoreOrder: true);
        var history = decided.GetProperty("history").EnumerateArray().ToArray();
        history.Select(h => h.GetProperty("toStatus").GetString()).ShouldContain("ManualReview");
        history[^1].GetProperty("comment").GetString()!.ShouldContain("Смоделированный ответ менеджера");
        history[^1].GetProperty("actor").GetString().ShouldBe("Manager");

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/feed?pageSize=50");
        feed.GetProperty("items").EnumerateArray()
            .Where(f => f.GetProperty("applicationId").ValueKind == JsonValueKind.String && f.GetProperty("applicationId").GetGuid() == id)
            .Select(f => f.GetProperty("title").GetString()!)
            .ShouldContain(t => t.Contains("одобрена менеджером"));

        var accepted = await client.PostAsync($"/api/applications/{id}/offer/accept", content: null);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterAccept = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        afterAccept.GetProperty("status").GetProperty("code").GetString().ShouldBe("ContractSigning");
        afterAccept.GetProperty("contract").GetProperty("amount").GetDecimal().ShouldBe(8_000_000);
        afterAccept.GetProperty("contract").GetProperty("interestRate").GetDecimal().ShouldBe(21m);

        // Повторное чтение не меняет решение
        var again = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        again.GetProperty("decision").GetProperty("decidedAt").GetString().ShouldBe(decision.GetProperty("decidedAt").GetString());
    }
}
