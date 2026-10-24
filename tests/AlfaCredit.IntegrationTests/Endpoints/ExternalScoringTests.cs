using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.Core.Features.Scoring;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

/// <summary>Внешний сервис скоринга по docs/scoring-contract.md: результат сохраняется, а при недоступности заявка не зависает.</summary>
[Collection(ExternalScoringCollection.Name)]
public sealed class ExternalScoringTests(ExternalScoringApiFactory factory)
{
    [DockerFact]
    public async Task External_scoring_result_is_used_and_stored()
    {
        factory.Scoring.Mode = StubScoringMode.Ok;
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 1_500_000, 12, "WorkingCapital");

        var settled = await client.WaitForDecisionAsync(id);

        settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("Approved");
        settled.GetProperty("decision").GetProperty("reasons").EnumerateArray().Select(r => r.GetProperty("code").GetString())
            .ShouldContain("factor_credit_history", "причины решения строятся на факторах внешнего скоринга");
        settled.GetProperty("offer").GetProperty("interestRate").GetDecimal().ShouldBe(19.5m, "грейд A: базовая ставка без надбавки");

        using var manager = await factory.CreateClient().LoginAsAsync("manager");
        var bankView = await manager.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        var scoring = bankView.GetProperty("scoring");
        scoring.GetProperty("source").GetString().ShouldBe("External");
        scoring.GetProperty("modelVersion").GetString().ShouldBe(StubScoringServer.ModelVersion);
        scoring.GetProperty("score").GetInt32().ShouldBe(82);
        scoring.GetProperty("grade").GetString().ShouldBe("A");
        scoring.GetProperty("probabilityOfDefault").GetDecimal().ShouldBe(0.04m);
        scoring.GetProperty("factors").GetArrayLength().ShouldBe(1);
        scoring.GetProperty("warnings").GetArrayLength().ShouldBe(0);
        scoring.GetProperty("missingData").EnumerateArray().Select(m => m.GetString()).ShouldBe(["company.history"]);
        scoring.GetProperty("durationMs").GetInt32().ShouldBeGreaterThanOrEqualTo(0);
        scoring.GetProperty("computedAt").GetDateTimeOffset().ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-5));
        var requestId = scoring.GetProperty("requestId").GetGuid();

        var sent = factory.Scoring.Requests.ToArray().Single(r => r.GetProperty("request_id").GetGuid() == requestId);
        var application = sent.GetProperty("application");
        application.GetProperty("number").GetString().ShouldBe(settled.GetProperty("number").GetString());
        application.GetProperty("amount_rub").GetInt64().ShouldBe(1_500_000);
        application.GetProperty("term_months").GetInt32().ShouldBe(12);
        application.GetProperty("estimated_monthly_payment_rub").GetInt64().ShouldBe(138_593, "аннуитет по базовой ставке, целые рубли");
        application.GetProperty("product").GetProperty("code").GetString().ShouldBe("working-capital");
        var company = sent.GetProperty("company");
        company.GetProperty("inn").GetString().ShouldBe("7701234567");
        company.GetProperty("region_code").GetString().ShouldBe("77");
        company.GetProperty("financials").GetProperty("revenue_rub").GetInt64().ShouldBe(48_000_000);
        var checks = sent.GetProperty("checks");
        checks.GetProperty("egrul").GetProperty("status").GetString().ShouldBe("Active");
        checks.GetProperty("credit_bureau").GetProperty("max_overdue_days_24m").GetInt32().ShouldBe(0);
        checks.GetProperty("fssp").GetProperty("open_proceedings").GetInt32().ShouldBe(0);
    }

    [DockerFact]
    public async Task Unavailable_scoring_falls_back_to_rules_and_does_not_block_the_application()
    {
        factory.Scoring.Mode = StubScoringMode.ServerError;
        try
        {
            using var client = await factory.CreateClient().LoginAsAsync("client_good");
            var companyId = await client.GetFirstCompanyIdAsync();
            var id = await client.CreateApplicationAsync(companyId, "working-capital", 1_500_000, 12, "WorkingCapital");

            var settled = await client.WaitForDecisionAsync(id);

            settled.GetProperty("status").GetProperty("code").GetString().ShouldBe("Approved");
            settled.GetProperty("offer").ValueKind.ShouldBe(JsonValueKind.Object);

            using var manager = await factory.CreateClient().LoginAsAsync("manager");
            var bankView = await manager.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
            var scoring = bankView.GetProperty("scoring");
            scoring.GetProperty("source").GetString().ShouldBe("RulesFallback");
            scoring.GetProperty("modelVersion").GetString().ShouldBe(RuleBasedScoringService.ModelVersion);
            scoring.GetProperty("warnings").EnumerateArray().Select(w => w.GetString()).ShouldContain("external_scoring_unavailable");
            scoring.GetProperty("grade").GetString().ShouldBe("A");
        }
        finally
        {
            factory.Scoring.Mode = StubScoringMode.Ok;
        }
    }
}
