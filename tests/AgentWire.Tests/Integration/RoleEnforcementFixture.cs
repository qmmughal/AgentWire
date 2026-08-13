using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

/// <summary>
/// IAsyncLifetime on a fixture (not a test class) runs InitializeAsync exactly once
/// per fixture instance - i.e. once per test class using IClassFixture&lt;T&gt; - unlike
/// putting IAsyncLifetime directly on the test class, where it reruns per test method.
/// </summary>
public sealed class RoleEnforcementFixture : AgentWireTestFactory, IAsyncLifetime
{
    public string AdminJwt { get; private set; } = null!;
    public string MemberJwt { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var client = CreateClient();
        var bootstrap = await client.BootstrapAsync(email: "role-admin@example.com");
        AdminJwt = bootstrap.Jwt;

        var createUserResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/v1/users")
        {
            Headers = { { "Authorization", $"Bearer {AdminJwt}" } },
            Content = JsonContent.Create(new { email = "role-member@example.com", tempPassword = "memberpass123", role = "Member" })
        });
        if (createUserResponse.StatusCode != HttpStatusCode.Created)
        {
            throw new System.Exception($"Failed to create member user: {createUserResponse.StatusCode}");
        }

        var loginResponse = await client.PostAsJsonAsync("/v1/auth/login", new { email = "role-member@example.com", password = "memberpass123" });
        var loginJson = await loginResponse.ReadJsonAsync();
        MemberJwt = loginJson.GetProperty("token").GetString()!;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}
