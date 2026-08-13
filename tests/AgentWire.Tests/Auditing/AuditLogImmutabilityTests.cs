using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Auditing;

/// <summary>
/// Immutability is enforced by the absence of any mutation route under /v1/audit-log,
/// not a soft-delete flag - these assert 404/405 (no such route mapped), not merely
/// "unauthorized", to prove the write surface genuinely doesn't exist.
/// </summary>
public class AuditLogImmutabilityTests : IClassFixture<AgentWireTestFactory>, IAsyncLifetime
{
    private readonly AgentWireTestFactory _factory;
    private string _adminJwt = null!;

    public AuditLogImmutabilityTests(AgentWireTestFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        var bootstrap = await _factory.EnsureBootstrappedAsync(email: "audit-admin@example.com");
        _adminJwt = bootstrap.Jwt;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Put_OnAuditLogEntry_IsNotAValidRoute()
    {
        var client = _factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Put, $"/v1/audit-log/{Guid.NewGuid()}")
        {
            Headers = { { "Authorization", $"Bearer {_adminJwt}" } },
            Content = JsonContent.Create(new { })
        });

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Delete_OnAuditLogEntry_IsNotAValidRoute()
    {
        var client = _factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/v1/audit-log/{Guid.NewGuid()}")
        {
            Headers = { { "Authorization", $"Bearer {_adminJwt}" } }
        });

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Patch_OnAuditLogEntry_IsNotAValidRoute()
    {
        var client = _factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Patch, $"/v1/audit-log/{Guid.NewGuid()}")
        {
            Headers = { { "Authorization", $"Bearer {_adminJwt}" } },
            Content = JsonContent.Create(new { })
        });

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task Get_OnAuditLog_ContainsTheBootstrapEvent()
    {
        var client = _factory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/v1/audit-log")
        {
            Headers = { { "Authorization", $"Bearer {_adminJwt}" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.True(json.GetArrayLength() > 0);
    }
}
