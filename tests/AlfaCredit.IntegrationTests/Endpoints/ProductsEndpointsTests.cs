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
