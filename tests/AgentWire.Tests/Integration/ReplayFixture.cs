using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

public sealed class ReplayFixture : AgentWireTestFactory, IAsyncLifetime
{
    public string ApiKey { get; private set; } = null!;
    public string Jwt { get; private set; } = null!;
    public string PacketId { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var client = CreateClient();
        var bootstrap = await client.BootstrapAsync(email: "replay-admin@example.com");
        ApiKey = bootstrap.ApiKey;
        Jwt = bootstrap.Jwt;

        var traceResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/v1/traces")
        {
            Headers = { { "X-API-Key", ApiKey } },
            Content = JsonContent.Create(new
            {
                traceId = "replay-trace",
                agentId = "agent",
                modelProvider = "openai",
                modelName = "gpt-4o-mini",
                systemPrompt = "You are helpful.",
                userPrompt = "Say hi.",
                llmResponse = "Hi!",
                promptTokens = 3,
                completionTokens = 2,
                latencyMs = 10
            })
        });
        var created = await traceResponse.ReadJsonAsync();
        PacketId = created.GetProperty("id").GetString()!;
    }

    Task IAsyncLifetime.DisposeAsync()
    {
        Dispose();
        return Task.CompletedTask;
    }
}
