using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AgentWire.Tests.Auth;

public class JwtIssuerTests : IDisposable
{
    private readonly string _tempDir;

    public JwtIssuerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"agentwire-jwt-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    private JwtIssuer CreateIssuer(int? expiryMinutes = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKeyFilePath"] = Path.Combine(_tempDir, "key.txt"),
                ["Jwt:Issuer"] = "agentwire-test",
                ["Jwt:Audience"] = "agentwire-api-test",
                ["Jwt:ExpiryMinutes"] = expiryMinutes?.ToString(),
            })
            .Build();

        return new JwtIssuer(config, NullLogger<JwtIssuer>.Instance);
    }

    [Fact]
    public void IssuedToken_RoundTripsWithCorrectClaims()
    {
        var issuer = CreateIssuer();
        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Email = "admin@acme.test",
            Role = UserRole.Admin,
            AuthProvider = AuthProviderType.Local
        };

        var token = issuer.IssueToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(user.Id.ToString(), jwt.Claims.Single(c => c.Type == "sub").Value);
        Assert.Equal(user.Email, jwt.Claims.Single(c => c.Type == "email").Value);
        Assert.Equal(user.OrganizationId.ToString(), jwt.Claims.Single(c => c.Type == "org_id").Value);
        Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == "role").Value);
        Assert.Equal("agentwire-test", jwt.Issuer);
    }

    [Fact]
    public void ExpiryMinutes_IsHonored()
    {
        var issuer = CreateIssuer(expiryMinutes: 5);
        var user = new AppUser { Id = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Email = "a@b.com" };

        var token = issuer.IssueToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        Assert.InRange(lifetime.TotalMinutes, 4.9, 5.1);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, true); } catch { }
    }
}
