using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AlfaCredit.IntegrationTests.Infrastructure;

internal static class HttpClientAuthExtensions
{
    /// <summary>Входит под демо-учёткой и подставляет JWT во все последующие запросы клиента.</summary>
    public static async Task<HttpClient> LoginAsAsync(this HttpClient client, string login, string password = ApiFactory.DemoPassword)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { login, password });
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = body.GetProperty("accessToken").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<Guid> GetFirstCompanyIdAsync(this HttpClient client)
    {
        var companies = await client.GetFromJsonAsync<JsonElement>("/api/companies");
        return companies.EnumerateArray().First().GetProperty("id").GetGuid();
    }
}
