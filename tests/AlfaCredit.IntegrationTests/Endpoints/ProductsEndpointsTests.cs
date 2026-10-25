using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AlfaCredit.IntegrationTests.Infrastructure;
using Shouldly;

namespace AlfaCredit.IntegrationTests.Endpoints;

[Collection(ApiCollection.Name)]
public sealed class ProductsEndpointsTests(ApiFactory factory)
{
    [DockerFact]
    public async Task Products_are_listed_without_authentication()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = await response.Content.ReadFromJsonAsync<JsonElement>();
        var codes = products.EnumerateArray().Select(p => p.GetProperty("code").GetString()).ToArray();
        codes.ShouldContain("working-capital");
        codes.ShouldContain("express");
        codes.Length.ShouldBe(5);

        var first = products.EnumerateArray().First();
        first.GetProperty("allowedPurposes").EnumerateArray().First().GetProperty("title").GetString().ShouldNotBeNullOrWhiteSpace();
        first.GetProperty("type").ValueKind.ShouldBe(JsonValueKind.String);
    }

    [DockerFact]
    public async Task Products_carry_a_preliminary_payment_for_the_requested_amount_and_term()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/products?amount=1500000&termMonths=12");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var products = (await response.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().ToDictionary(p => p.GetProperty("code").GetString()!);
        products.Count.ShouldBe(5);

        var workingCapital = products["working-capital"].GetProperty("preliminaryPayment");
        workingCapital.GetProperty("available").GetBoolean().ShouldBeTrue();
        workingCapital.GetProperty("amount").GetDecimal().ShouldBe(1_500_000);
        workingCapital.GetProperty("termMonths").GetInt32().ShouldBe(12);
        workingCapital.GetProperty("interestRate").GetDecimal().ShouldBe(19.5m);
        workingCapital.GetProperty("monthlyPayment").GetDecimal().ShouldBe(138_593.06m, "как в preview и в предложении банка");
        workingCapital.GetProperty("totalPayment").GetDecimal().ShouldBe(1_663_116.74m);
        workingCapital.GetProperty("totalInterest").GetDecimal().ShouldBe(163_116.74m);
        var note = workingCapital.GetProperty("notes").EnumerateArray().Single();
        note.GetProperty("code").GetString().ShouldBe("preliminary");
        note.GetProperty("text").GetString()!.ShouldContain("19,5 % годовых");

        var express = products["express"].GetProperty("preliminaryPayment");
        express.GetProperty("available").GetBoolean().ShouldBeTrue();
        express.GetProperty("monthlyPayment").GetDecimal().ShouldBe(141_114.48m);

        var investment = products["investment"].GetProperty("preliminaryPayment");
        investment.GetProperty("available").GetBoolean().ShouldBeTrue();
        investment.GetProperty("totalPayment").GetDecimal().ShouldBe(1_500_000 + investment.GetProperty("totalInterest").GetDecimal());

        foreach (var code in new[] { "overdraft", "credit-line" })
        {
            var limit = products[code].GetProperty("preliminaryPayment");
            limit.GetProperty("available").GetBoolean().ShouldBeFalse(code);
            limit.GetProperty("monthlyPayment").ValueKind.ShouldBe(JsonValueKind.Null, code);
            limit.GetProperty("totalPayment").ValueKind.ShouldBe(JsonValueKind.Null, code);
            limit.GetProperty("totalInterest").ValueKind.ShouldBe(JsonValueKind.Null, code);
            limit.GetProperty("notes").EnumerateArray().Single().GetProperty("code").GetString().ShouldBe("limit_product", code);
        }

        // Без параметров расчёта нет, формат остальных полей прежний
        var plain = await client.GetFromJsonAsync<JsonElement>("/api/products");
        plain.EnumerateArray().ShouldAllBe(p => p.GetProperty("preliminaryPayment").ValueKind == JsonValueKind.Null);
    }

    [DockerFact]
    public async Task Product_by_code_explains_why_the_payment_is_unavailable_and_rejects_half_requests()
    {
        using var client = factory.CreateClient();

        var tooBig = await client.GetFromJsonAsync<JsonElement>("/api/products/express?amount=5000000&termMonths=24");
        var payment = tooBig.GetProperty("preliminaryPayment");
        payment.GetProperty("available").GetBoolean().ShouldBeFalse();
        payment.GetProperty("amount").GetDecimal().ShouldBe(5_000_000);
        payment.GetProperty("notes").EnumerateArray().Select(n => n.GetProperty("code").GetString()).ShouldBe(["amount_above_max", "term_out_of_range"]);

        var halfRequest = await client.GetAsync("/api/products/express?amount=1500000");
        halfRequest.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await halfRequest.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").GetProperty("termMonths")[0].GetString()!.ShouldContain("вместе с суммой");

        var zeroAmount = await client.GetAsync("/api/products?amount=0&termMonths=12");
        zeroAmount.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await zeroAmount.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("amount", out _).ShouldBeTrue();

        var longTerm = await client.GetAsync("/api/products?amount=1500000&termMonths=121");
        longTerm.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await longTerm.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("termMonths", out _).ShouldBeTrue();

        var missing = await client.GetAsync("/api/products/no-such-product?amount=1500000&termMonths=12");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [DockerFact]
    public async Task Product_can_be_fetched_by_code_and_unknown_code_is_404()
    {
        using var client = factory.CreateClient();

        var found = await client.GetAsync("/api/products/Working-Capital");
        var missing = await client.GetAsync("/api/products/no-such-product");

        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await found.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("name").GetString().ShouldBe("Оборотный кредит");
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString().ShouldBe("Продукт не найден");
    }
}
