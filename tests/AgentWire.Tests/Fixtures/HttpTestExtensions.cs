using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgentWire.Tests.Fixtures;

public static class HttpTestExtensions
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(text);
        return doc.RootElement.Clone();
    }

    public sealed record BootstrapResult(string ApiKey, string Jwt, string OrganizationId, string AdminUserId, string AdminEmail);

    public static async Task<BootstrapResult> BootstrapAsync(
        this HttpClient client,
        string orgName = "Test Org",
        string email = "admin@test.local",
        string password = "supersecret123")
    {
        var response = await client.PostAsJsonAsync("/v1/setup", new
        {
            organizationName = orgName,
            adminEmail = email,
            adminPassword = password
        });
        response.EnsureSuccessStatusCode();
        var json = await response.ReadJsonAsync();

        return new BootstrapResult(
            json.GetProperty("apiKey").GetString()!,
            json.GetProperty("jwt").GetString()!,
            json.GetProperty("organizationId").GetString()!,
            json.GetProperty("adminUserId").GetString()!,
            json.GetProperty("adminEmail").GetString()!);
    }
}
