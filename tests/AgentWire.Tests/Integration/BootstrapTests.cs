using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

/// <summary>
/// Each test method gets its own AgentWireTestFactory (constructed fresh per test,
/// not shared via IClassFixture) because these tests specifically exercise the
/// "only the first /v1/setup call succeeds" guarantee and must not see bootstrap
/// state left over by a sibling test.
/// </summary>
public class BootstrapTests : IDisposable
{
    private readonly AgentWireTestFactory _factory = new();

    [Fact]
    public async Task FirstSetupCall_Succeeds_AndReturnsUsableCredentials()
    {
        var client = _factory.CreateClient();

        var result = await client.BootstrapAsync(orgName: "First Org", email: "first@example.com");

        Assert.NotEmpty(result.ApiKey);
        Assert.NotEmpty(result.Jwt);
        Assert.NotEmpty(result.OrganizationId);

        var me = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/v1/auth/me")
        {
            Headers = { { "Authorization", $"Bearer {result.Jwt}" } }
        });
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task SecondSetupCall_Returns409Conflict()
    {
        var client = _factory.CreateClient();
        await client.BootstrapAsync(orgName: "Only Org", email: "only@example.com");

        var second = await client.PostAsJsonAsync("/v1/setup", new
        {
            organizationName = "Another Org",
            adminEmail = "another@example.com",
            adminPassword = "supersecret123"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
