using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Application.Replay;
using AgentWire.Infrastructure.Replay;
using AgentWire.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentWire.Tests.Integration;

public class ReplayTests : IClassFixture<ReplayFixture>
{
    private readonly ReplayFixture _fixture;

    public ReplayTests(ReplayFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Replay_WithNoProviderConfigured_Returns422_AndCreatesFailedResult()
    {
        var client = _fixture.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/v1/packets/{_fixture.PacketId}/replay")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.Jwt}" } },
            Content = JsonContent.Create(new { })
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        var replaysResponse = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/v1/packets/{_fixture.PacketId}/replays")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.Jwt}" } }
        });
        var replays = await replaysResponse.ReadJsonAsync();
        Assert.True(replays.GetArrayLength() >= 1);
        Assert.Equal("Failed", replays[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Replay_WithConfiguredProvider_Returns200_AndPersistsLinkedPacket()
    {
        using var configuredFactory = _fixture.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Replay:BaseUrl"] = "https://stub-provider.test/v1",
                });
            });
            builder.ConfigureServices(services =>
            {
                services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>()
                    .ConfigurePrimaryHttpMessageHandler(() => StubHttpMessageHandler.OpenAiStyleSuccess("Stubbed reply"));
            });
        });

        var client = configuredFactory.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/v1/packets/{_fixture.PacketId}/replay")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.Jwt}" } },
            Content = JsonContent.Create(new { })
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.ReadJsonAsync();
        Assert.Equal("Stubbed reply", body.GetProperty("response").GetString());

        var newPacketId = body.GetProperty("newPacketId").GetString();
        var getNewPacket = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/v1/packets/{newPacketId}")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.Jwt}" } }
        });
        Assert.Equal(HttpStatusCode.OK, getNewPacket.StatusCode);
        var newPacket = await getNewPacket.ReadJsonAsync();
        Assert.Equal(_fixture.PacketId, newPacket.GetProperty("replayOfPacketId").GetString());
    }
}
