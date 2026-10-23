using System.Net.Http.Json;
using System.Text.Json;

namespace AlfaCredit.IntegrationTests.Infrastructure;

internal static class PipelineHelpers
{
    private static readonly HashSet<string> SettledStatuses =
    [
        "Approved", "ApprovedWithConditions", "Rejected", "ManualReview", "NeedsClarification", "Cancelled", "OfferDeclined",
    ];

    /// <summary>Ждёт, пока фоновый конвейер доведёт заявку до решения или до состояния, требующего участия человека.</summary>
    public static async Task<JsonElement> WaitForDecisionAsync(this HttpClient client, Guid applicationId, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        JsonElement last = default;
        while (DateTime.UtcNow < deadline)
        {
            last = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{applicationId}");
            var status = last.GetProperty("status").GetProperty("code").GetString()!;
            if (SettledStatuses.Contains(status))
            {
                return last;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Application {applicationId} did not settle in time; last status: {last.GetProperty("status").GetProperty("code").GetString()}");
    }

    /// <summary>Ждёт конкретный статус, например Disbursed после подписания договора.</summary>
    public static async Task<JsonElement> WaitForStatusAsync(this HttpClient client, Guid applicationId, string status, TimeSpan? timeout = null)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        JsonElement last = default;
        while (DateTime.UtcNow < deadline)
        {
            last = await client.GetFromJsonAsync<JsonElement>($"/api/applications/{applicationId}");
            if (last.GetProperty("status").GetProperty("code").GetString() == status)
            {
                return last;
            }

            await Task.Delay(250);
        }

        throw new TimeoutException($"Application {applicationId} did not reach {status}; last status: {last.GetProperty("status").GetProperty("code").GetString()}");
    }

    public static async Task<Guid> CreateApplicationAsync(this HttpClient client, Guid companyId, string productCode, decimal amount, int termMonths, string purpose)
    {
        var response = await client.PostAsJsonAsync("/api/applications", new { companyId, productCode, amount, termMonths, purpose });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
