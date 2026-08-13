using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace AgentWire.Tests.Integration;

/// <summary>
/// Asserts BOTH directions (200-for-Admin and 403-for-Member) on the same endpoints,
/// not just one. A JWT role-claim-mapping misconfiguration (see JwtBearerOptions in
/// Program.cs - MapInboundClaims=false is required) tends to manifest as authorization
/// silently allowing or denying everyone, which a single-direction test would miss.
/// </summary>
public class RoleEnforcementTests : IClassFixture<RoleEnforcementFixture>
{
    private readonly RoleEnforcementFixture _fixture;

    public RoleEnforcementTests(RoleEnforcementFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData("/v1/audit-log")]
    [InlineData("/v1/users")]
    [InlineData("/v1/apikeys")]
    public async Task Admin_CanAccessAdminOnlyEndpoints(string path)
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, path)
        {
            Headers = { { "Authorization", $"Bearer {_fixture.AdminJwt}" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/v1/audit-log")]
    [InlineData("/v1/users")]
    [InlineData("/v1/apikeys")]
    public async Task Member_IsForbiddenFromAdminOnlyEndpoints(string path)
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, path)
        {
            Headers = { { "Authorization", $"Bearer {_fixture.MemberJwt}" } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Member_CanAccessNonAdminEndpoints()
    {
        var client = _fixture.CreateClient();
        var response = await client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "/v1/packets")
        {
            Headers = { { "Authorization", $"Bearer {_fixture.MemberJwt}" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
