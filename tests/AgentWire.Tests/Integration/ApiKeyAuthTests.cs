using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

public class ApiKeyAuthTests : IClassFixture<AgentWireTestFactory>, IAsyncLifetime
{
    private readonly AgentWireTestFactory _factory;
    private HttpTestExtensions.BootstrapResult _bootstrap = null!;

    public ApiKeyAuthTests(AgentWireTestFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _bootstrap = await _factory.EnsureBootstrappedAsync(email: "apikey-admin@example.com");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static object SamplePacket() => new
    {
        traceId = "t-1",
        agentId = "agent-1",
        modelProvider = "openai",
        modelName = "gpt-4o-mini",
        systemPrompt = "sys",
        userPrompt = "hello",
        llmResponse = "hi",
        promptTokens = 1,
        completionTokens = 1,
        latencyMs = 5
    };

    [Fact]
    public async Task ValidApiKey_Returns202()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/traces")
        {
            Headers = { { "X-API-Key", _bootstrap.ApiKey } },
            Content = JsonContent.Create(SamplePacket())
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/traces", SamplePacket());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/traces")
        {
            Headers = { { "X-API-Key", "aw_live_not_a_real_key" } },
            Content = JsonContent.Create(SamplePacket())
        };

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RevokedApiKey_Returns401()
    {
        var client = _factory.CreateClient();

        var createKeyResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/v1/apikeys")
        {
            Headers = { { "Authorization", $"Bearer {_bootstrap.Jwt}" } },
            Content = JsonContent.Create(new { name = "revoke-me" })
        });
        var created = await createKeyResponse.ReadJsonAsync();
        var keyId = created.GetProperty("id").GetString();
        var rawKey = created.GetProperty("key").GetString();

        var revokeResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/v1/apikeys/{keyId}/revoke")
        {
            Headers = { { "Authorization", $"Bearer {_bootstrap.Jwt}" } }
        });
        Assert.Equal(HttpStatusCode.OK, revokeResponse.StatusCode);

        var traceRequest = new HttpRequestMessage(HttpMethod.Post, "/v1/traces")
        {
            Headers = { { "X-API-Key", rawKey! } },
            Content = JsonContent.Create(SamplePacket())
        };
        var traceResponse = await client.SendAsync(traceRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, traceResponse.StatusCode);
    }
}
