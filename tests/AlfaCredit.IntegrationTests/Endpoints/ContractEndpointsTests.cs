using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

/// <summary>Завершение заявки по docs/completion-flow.md: предложение, договор, подписание кодом, выдача.</summary>
[Collection(ApiCollection.Name)]
public sealed class ContractEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Client_accepts_offer_signs_contract_with_code_and_receives_disbursement()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_good");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 1_500_000, 12, "WorkingCapital");
        var approved = await client.WaitForDecisionAsync(id);
        approved.GetProperty("status").GetProperty("code").GetString().ShouldBe("Approved");
        approved.GetProperty("contract").ValueKind.ShouldBe(JsonValueKind.Null);
        approved.GetProperty("disbursement").ValueKind.ShouldBe(JsonValueKind.Null);
        (await client.GetAsync($"/api/applications/{id}/contract")).StatusCode.ShouldBe(HttpStatusCode.NotFound, "договора нет до принятия предложения");

        // Принятие предложения формирует договор
        var accepted = await client.PostAsync($"/api/applications/{id}/offer/accept", content: null);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterAccept = await accepted.Content.ReadFromJsonAsync<JsonElement>();
        afterAccept.GetProperty("status").GetProperty("code").GetString().ShouldBe("ContractSigning");
        afterAccept.GetProperty("availableActions").EnumerateArray().Select(a => a.GetString()).ShouldBe(["signContract", "cancel"], ignoreOrder: true);
        var summary = afterAccept.GetProperty("contract");
        var number = summary.GetProperty("number").GetString()!;
        number.ShouldStartWith("KD-");
        summary.GetProperty("status").GetString().ShouldBe("AwaitingSignature");
        summary.GetProperty("amount").GetDecimal().ShouldBe(1_500_000);
        summary.GetProperty("interestRate").GetDecimal().ShouldBe(19.5m);
        summary.GetProperty("monthlyPayment").GetDecimal().ShouldBe(138_593.06m);
        summary.GetProperty("totalInterest").GetDecimal().ShouldBeGreaterThan(0);
        summary.GetProperty("disbursementAccount").GetString()!.Length.ShouldBe(20);
        summary.GetProperty("signing").GetProperty("canRequestCode").GetBoolean().ShouldBeTrue();
        summary.GetProperty("signing").GetProperty("codeRequestedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        summary.GetProperty("signing").GetProperty("attemptsLeft").GetInt32().ShouldBe(5);
        summary.GetProperty("documentUrl").GetString().ShouldBe($"/api/applications/{id}/contract/document");

        // Полный договор и печатная форма
        var contract = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}/contract");
        contract.GetProperty("number").GetString().ShouldBe(number);
        contract.GetProperty("borrower").GetProperty("inn").GetString().ShouldBe("7701234567");
        contract.GetProperty("lender").GetProperty("name").GetString().ShouldNotBeNullOrWhiteSpace();
        contract.GetProperty("product").GetProperty("code").GetString().ShouldBe("working-capital");
        var schedule = contract.GetProperty("schedule");
        schedule.GetArrayLength().ShouldBe(12);
        schedule[0].GetProperty("number").GetInt32().ShouldBe(1);
        schedule[0].GetProperty("interest").GetDecimal().ShouldBe(24_375m);
        schedule[11].GetProperty("balance").GetDecimal().ShouldBe(0);
        contract.GetProperty("signedBy").ValueKind.ShouldBe(JsonValueKind.Null);

        var document = await client.GetAsync($"/api/applications/{id}/contract/document");
        document.StatusCode.ShouldBe(HttpStatusCode.OK);
        document.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        var html = await document.Content.ReadAsStringAsync();
        html.ShouldContain(number);
        html.ShouldContain("Кредитный договор");
        html.ShouldContain("Ромашка");

        // Подписание: без кода 409, неверный формат 400, неверный код 400 с учётом попыток
        (await client.PostAsJsonAsync($"/api/applications/{id}/contract/sign", new { code = "123456" })).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var malformed = await client.PostAsJsonAsync($"/api/applications/{id}/contract/sign", new { code = "12" });
        malformed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await malformed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("code", out _).ShouldBeTrue();

        var codeResponse = await client.PostAsync($"/api/applications/{id}/contract/sign/request-code", content: null);
        codeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var issued = await codeResponse.Content.ReadFromJsonAsync<JsonElement>();
        issued.GetProperty("contractNumber").GetString().ShouldBe(number);
        issued.GetProperty("deliveredVia").GetString().ShouldBe("Feed");
        issued.GetProperty("attemptsLeft").GetInt32().ShouldBe(5);
        var code = issued.GetProperty("demoCode").GetString()!;
        code.Length.ShouldBe(6);

        var feed = await client.GetFromJsonAsync<JsonElement>("/api/feed?pageSize=50");
        feed.GetProperty("items").EnumerateArray()
            .Where(f => f.GetProperty("type").GetString() == "SigningCode")
            .Select(f => f.GetProperty("body").GetString()!)
            .ShouldContain(body => body.Contains(code), "код доставлен в ленту (имитация SMS)");

        var wrongCode = code == "000000" ? "111111" : "000000";
        var wrong = await client.PostAsJsonAsync($"/api/applications/{id}/contract/sign", new { code = wrongCode });
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrong.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("code")[0].GetString()!.ShouldContain("Осталось попыток: 4");
        var afterWrong = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{id}");
        afterWrong.GetProperty("contract").GetProperty("signing").GetProperty("attemptsLeft").GetInt32().ShouldBe(4);
        afterWrong.GetProperty("contract").GetProperty("signing").GetProperty("codeRequestedAt").ValueKind.ShouldBe(JsonValueKind.String);

        var signed = await client.PostAsJsonAsync($"/api/applications/{id}/contract/sign", new { code });
        signed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterSign = await signed.Content.ReadFromJsonAsync<JsonElement>();
        afterSign.GetProperty("status").GetProperty("code").GetString().ShouldBe("Signed");
        afterSign.GetProperty("contract").GetProperty("status").GetString().ShouldBe("Signed");
        afterSign.GetProperty("contract").GetProperty("signedAt").ValueKind.ShouldBe(JsonValueKind.String);
        afterSign.GetProperty("availableActions").GetArrayLength().ShouldBe(0);

        // Выдача выполняется воркером
        var disbursed = await client.WaitForStatusAsync(id, "Disbursed");
        disbursed.GetProperty("status").GetProperty("isTerminal").GetBoolean().ShouldBeTrue();
        var disbursement = disbursed.GetProperty("disbursement");
        disbursement.GetProperty("status").GetString().ShouldBe("Completed");
        disbursement.GetProperty("paymentOrderNumber").GetString()!.ShouldStartWith("PO-");
        disbursement.GetProperty("amount").GetDecimal().ShouldBe(1_500_000);
        disbursement.GetProperty("account").GetString().ShouldBe(summary.GetProperty("disbursementAccount").GetString());
        disbursement.GetProperty("completedAt").ValueKind.ShouldBe(JsonValueKind.String);
        disbursed.GetProperty("history").EnumerateArray().Select(h => h.GetProperty("toStatus").GetString()).ShouldBe(
            ["Submitted", "Validating", "Enriching", "Scoring", "Deciding", "Approved", "ContractSigning", "Signed", "Disbursed"]);
        var feedAfter = await client.GetFromJsonAsync<JsonElement>("/api/feed?pageSize=50");
        feedAfter.GetProperty("items").EnumerateArray().Select(f => f.GetProperty("type").GetString()).ShouldContain("Disbursed");

        // После выдачи подписывать нечего; менеджер видит договор, но не подписывает
        (await client.PostAsync($"/api/applications/{id}/contract/sign/request-code", content: null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var manager = await factory.CreateClient().LoginAsAsync("manager");
        var bankContract = await manager.GetFromJsonAsync<JsonElement>($"/api/applications/{id}/contract");
        bankContract.GetProperty("signedBy").GetProperty("fullName").GetString().ShouldNotBeNullOrWhiteSpace();
        bankContract.GetProperty("signatureMethod").GetString().ShouldNotBeNullOrWhiteSpace();
        (await manager.PostAsync($"/api/applications/{id}/contract/sign/request-code", content: null)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var signedHtml = await (await manager.GetAsync($"/api/applications/{id}/contract/document")).Content.ReadAsStringAsync();
        signedHtml.ShouldContain("Подписано");
        signedHtml.ShouldNotContain(code);
    }

    [DockerFact]
    public async Task Client_can_withdraw_from_signing()
    {
        using var client = await factory.CreateClient().LoginAsAsync("client_medium");
        var companyId = await client.GetFirstCompanyIdAsync();
        var id = await client.CreateApplicationAsync(companyId, "working-capital", 3_000_000, 24, "WorkingCapital");
        (await client.WaitForDecisionAsync(id)).GetProperty("status").GetProperty("code").GetString().ShouldBe("ApprovedWithConditions");

        var accepted = await (await client.PostAsync($"/api/applications/{id}/offer/accept", content: null)).Content.ReadFromJsonAsync<JsonElement>();
        accepted.GetProperty("contract").GetProperty("amount").GetDecimal().ShouldBe(2_400_000, "договор на условиях предложения, не заявки");
        accepted.GetProperty("contract").GetProperty("interestRate").GetDecimal().ShouldBe(22.5m);

        var cancelled = await client.PostAsJsonAsync($"/api/applications/{id}/cancel", new { reason = "Передумали" });
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await cancelled.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("status").GetProperty("code").GetString().ShouldBe("Cancelled");
        body.GetProperty("contract").GetProperty("status").GetString().ShouldBe("Cancelled");
        body.GetProperty("contract").GetProperty("signing").GetProperty("canRequestCode").GetBoolean().ShouldBeFalse();

        (await client.PostAsync($"/api/applications/{id}/contract/sign/request-code", content: null)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await client.GetAsync($"/api/applications/{id}/contract")).StatusCode.ShouldBe(HttpStatusCode.OK, "договор остаётся доступен для просмотра");
    }
}
