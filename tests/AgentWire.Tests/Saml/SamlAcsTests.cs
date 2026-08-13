using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AgentWire.Infrastructure.Data;
using AgentWire.Tests.Fixtures;
using ITfoxtec.Identity.Saml2;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentWire.Tests.Saml;

/// <summary>
/// Proves SP-side XML-dsig validation and the find-or-provision-then-issue-JWT
/// pipeline work correctly at the wire-protocol level, using a hand-signed test
/// assertion from a locally-generated fake IdP certificate (see
/// SamlTestAssertionBuilder). This does NOT prove interoperability with any specific
/// real-world IdP (Okta, Entra ID, Keycloak) - only a manual test against a real IdP
/// tenant can catch metadata-format or attribute-naming differences.
/// </summary>
public class SamlAcsTests : IClassFixture<AgentWireTestFactory>
{
    private readonly AgentWireTestFactory _factory;

    public SamlAcsTests(AgentWireTestFactory factory)
    {
        _factory = factory;

        // Bootstrap so there's an organization for SSO-provisioned users to land in,
        // and set it as the default (mirrors what an operator configures in appsettings).
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
        if (!db.Organizations.Any())
        {
            _factory.CreateClient().BootstrapAsync(email: "saml-admin@example.com").GetAwaiter().GetResult();
        }
    }

    private async Task<HttpResponseMessage> PostAcsAsync(string samlResponseBase64)
    {
        var client = _factory.CreateClient();
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["SAMLResponse"] = samlResponseBase64
        });
        return await client.PostAsync("/saml/acs", content);
    }

    [Fact]
    public async Task ValidlySignedAssertion_FromTrustedIdp_ProvisionsUserAndReturnsToken()
    {
        var fakeIdpCert = SamlTestAssertionBuilder.CreateSigningCertificate();
        var saml2Config = _factory.Services.GetRequiredService<Saml2Configuration>();

        const string idpIssuer = "https://fake-idp.test/trusted";
        lock (saml2Config)
        {
            saml2Config.SignatureValidationCertificates.Add(fakeIdpCert);
            saml2Config.AllowedIssuer = idpIssuer;
        }

        var email = $"saml-user-{System.Guid.NewGuid():N}@example.com";
        var responseBase64 = SamlTestAssertionBuilder.BuildSignedResponseBase64(
            fakeIdpCert,
            idpIssuer: idpIssuer,
            spAudience: saml2Config.Issuer,
            acsDestination: "http://localhost/saml/acs",
            subjectEmail: email);

        var response = await PostAcsAsync(responseBase64);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("token", body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
        Assert.NotNull(user);
        Assert.Equal(AgentWire.Core.Enums.AuthProviderType.Saml, user!.AuthProvider);

        var auditEntry = await db.AuditLogEntries
            .Where(a => a.EventType == "auth.login.saml.success" && a.ActorEmail == email)
            .FirstOrDefaultAsync();
        Assert.NotNull(auditEntry);
    }

    [Fact]
    public async Task AssertionSignedByUntrustedCertificate_IsRejected_NoUserCreated()
    {
        var saml2Config = _factory.Services.GetRequiredService<Saml2Configuration>();
        var untrustedCert = SamlTestAssertionBuilder.CreateSigningCertificate("CN=Untrusted Fake IdP");
        // Deliberately NOT added to saml2Config.SignatureValidationCertificates.

        var email = $"saml-untrusted-{System.Guid.NewGuid():N}@example.com";
        var responseBase64 = SamlTestAssertionBuilder.BuildSignedResponseBase64(
            untrustedCert,
            idpIssuer: "https://not-a-trusted-idp.test",
            spAudience: saml2Config.Issuer,
            acsDestination: "http://localhost/saml/acs",
            subjectEmail: email);

        var response = await PostAcsAsync(responseBase64);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgentWireDbContext>();
        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
        Assert.Null(user);
    }
}
