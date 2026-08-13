using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AgentWire.Tests.Fixtures;
using Xunit;

namespace AgentWire.Tests.Integration;

public class LocalLoginTests : IClassFixture<AgentWireTestFactory>, IAsyncLifetime
{
    private readonly AgentWireTestFactory _factory;
    private HttpTestExtensions.BootstrapResult _bootstrap = null!;

    public LocalLoginTests(AgentWireTestFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _bootstrap = await _factory.EnsureBootstrappedAsync(email: "login-admin@example.com", password: "supersecret123");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CorrectCredentials_ReturnsToken()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/login", new
        {
            email = _bootstrap.AdminEmail,
            password = "supersecret123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.ReadJsonAsync();
        Assert.False(string.IsNullOrEmpty(json.GetProperty("token").GetString()));
    }

    [Fact]
    public async Task WrongPassword_Returns401()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/login", new
        {
            email = _bootstrap.AdminEmail,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnknownEmail_Returns401_SameAsWrongPassword()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/v1/auth/login", new
        {
            email = "nobody-at-all@example.com",
            password = "whatever123"
        });

        // Deliberately the same status/shape as WrongPassword - no user-enumeration signal.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Empty(body);
    }
}
