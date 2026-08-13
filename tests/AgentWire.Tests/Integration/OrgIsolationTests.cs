using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

public class OrgIsolationTests : IClassFixture<OrgIsolationFixture>
{
    private readonly OrgIsolationFixture _fixture;

    public OrgIsolationTests(OrgIsolationFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task OrgB_CannotListOrgAsPackets()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/v1/packets")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.OrgBJwt}" } }
        });

        var packets = await response.ReadJsonAsync();
        Assert.Equal(JsonValueKind.Array, packets.ValueKind);
        Assert.Equal(0, packets.GetArrayLength());
    }

    [Fact]
    public async Task OrgB_CannotFetchOrgAsPacketById_Gets404NotOrgAsData()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/v1/packets/{_fixture.OrgAPacketId}")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.OrgBJwt}" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrgB_CannotSeeOrgAsFindings_ByGuessingPacketId()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/v1/packets/{_fixture.OrgAPacketId}/findings")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.OrgBJwt}" } }
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OrgA_CanSeeItsOwnPacket()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, $"/v1/packets/{_fixture.OrgAPacketId}")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.OrgAJwt}" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OrgB_CannotReplayOrgAsPacket()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Post, $"/v1/packets/{_fixture.OrgAPacketId}/replay")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.OrgBJwt}" } },
            Content = JsonContent.Create(new { })
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
