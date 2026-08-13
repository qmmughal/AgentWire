using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AgentWire.Application.Auth;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Auditing;
using AgentWire.Infrastructure.Auth;
using AgentWire.Tests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AgentWire.Tests.Auth;

public class UserProvisioningServiceTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();
    private readonly Organization _org;

    public UserProvisioningServiceTests()
    {
        _org = new Organization { Name = "Test Org", Slug = "test-org-" + Guid.NewGuid().ToString("N")[..6] };
        _factory.Db.Organizations.Add(_org);
        _factory.Db.SaveChanges();
    }

    private UserProvisioningService CreateService()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sso:DefaultOrganizationId"] = _org.Id.ToString(),
            })
            .Build();

        return new UserProvisioningService(_factory.Db, config, new AuditLogWriter(_factory.Db));
    }

    private static ClaimsPrincipal PrincipalWithEmail(string email, string? name = null)
    {
        var claims = new List<Claim> { new(ClaimTypes.Email, email) };
        if (name is not null)
        {
            claims.Add(new Claim(ClaimTypes.Name, name));
        }
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    [Fact]
    public async Task NewIdentity_CreatesMemberInDefaultOrg()
    {
        var service = CreateService();
        var user = await service.FindOrProvisionAsync(PrincipalWithEmail("new.user@example.com"), "Oidc");

        Assert.Equal(_org.Id, user.OrganizationId);
        Assert.Equal(UserRole.Member, user.Role);
        Assert.Equal(AuthProviderType.Oidc, user.AuthProvider);
        Assert.Equal("new.user@example.com", user.Email);
    }

    [Fact]
    public async Task RepeatedLogin_IsIdempotent_NoDuplicateUser()
    {
        var service = CreateService();
        var first = await service.FindOrProvisionAsync(PrincipalWithEmail("repeat@example.com"), "Oidc");
        var second = await service.FindOrProvisionAsync(PrincipalWithEmail("repeat@example.com"), "Oidc");

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, _factory.Db.AppUsers.Count(u => u.Email == "repeat@example.com"));
    }

    [Fact]
    public async Task MissingEmailClaim_ThrowsSsoProvisioningException()
    {
        var service = CreateService();
        var principalWithoutEmail = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "No Email")], "test"));

        await Assert.ThrowsAsync<SsoProvisioningException>(() => service.FindOrProvisionAsync(principalWithoutEmail, "Oidc"));
    }

    [Fact]
    public async Task NoOrganizationConfigured_ThrowsSsoProvisioningException()
    {
        var config = new ConfigurationBuilder().Build(); // no Sso:DefaultOrganizationId, no orgs seeded
        using var emptyFactory = new TestDbContextFactory();
        var service = new UserProvisioningService(emptyFactory.Db, config, new AuditLogWriter(emptyFactory.Db));

        await Assert.ThrowsAsync<SsoProvisioningException>(
            () => service.FindOrProvisionAsync(PrincipalWithEmail("nobody@example.com"), "Oidc"));
    }

    public void Dispose() => _factory.Dispose();
}
