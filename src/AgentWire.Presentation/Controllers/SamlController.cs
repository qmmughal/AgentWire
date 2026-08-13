using System;
using System.Security.Claims;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Infrastructure.Data;
using ITfoxtec.Identity.Saml2;
using ITfoxtec.Identity.Saml2.MvcCore;
using ITfoxtec.Identity.Saml2.Schemas;
using ITfoxtec.Identity.Saml2.Schemas.Metadata;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Presentation.Controllers;

/// <summary>
/// SP-side SAML 2.0 (single upstream IdP per deployment - see Saml2ConfigurationFactory).
/// Validated by SamlAcsTests, which hand-signs a test assertion with a locally-generated
/// fake-IdP certificate and posts it to Acs(). That proves SP-side XML-dsig validation and
/// the find-or-provision-then-issue-JWT pipeline work correctly at the wire-protocol level.
/// It does NOT prove interoperability with any specific real-world IdP (Okta, Entra ID,
/// Keycloak, etc.) - those differ in metadata format, attribute naming, and encrypted-
/// assertion defaults, which only a manual test against a real IdP tenant can catch.
/// Single Logout is out of scope: with a stateless JWT design, logout is client-side token
/// discard.
/// </summary>
[AllowAnonymous]
[Route("saml")]
public sealed class SamlController : Controller
{
    private readonly Saml2Configuration _config;
    private readonly IUserProvisioningService _provisioning;
    private readonly IJwtIssuer _jwtIssuer;
    private readonly IAuditLogWriter _auditLog;
    private readonly AgentWireDbContext _db;

    public SamlController(
        Saml2Configuration config,
        IUserProvisioningService provisioning,
        IJwtIssuer jwtIssuer,
        IAuditLogWriter auditLog,
        AgentWireDbContext db)
    {
        _config = config;
        _provisioning = provisioning;
        _jwtIssuer = jwtIssuer;
        _auditLog = auditLog;
        _db = db;
    }

    private string DefaultSite => $"{Request.Scheme}://{Request.Host.ToUriComponent()}";

    [HttpGet("metadata")]
    public IActionResult Metadata()
    {
        var entityDescriptor = new EntityDescriptor(_config, signMetadata: true)
        {
            ValidUntil = 365,
            SPSsoDescriptor = new SPSsoDescriptor
            {
                WantAssertionsSigned = true,
                SigningCertificates = [_config.SigningCertificate],
                NameIDFormats = [NameIdentifierFormats.Email],
                AssertionConsumerServices =
                [
                    new AssertionConsumerService
                    {
                        Binding = new Uri("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST"),
                        Location = new Uri($"{DefaultSite}/saml/acs"),
                        IsDefault = true
                    }
                ]
            }
        };

        return new Saml2Metadata(entityDescriptor).CreateMetadata().ToActionResult();
    }

    [HttpGet("login")]
    public IActionResult Login()
    {
        if (_config.SingleSignOnDestination is null)
        {
            return NotFound(new { error = "SAML SSO is not configured on this instance (no IdP metadata loaded)." });
        }

        var binding = new Saml2RedirectBinding();
        return binding.Bind(new Saml2AuthnRequest(_config)
        {
            AssertionConsumerServiceUrl = new Uri($"{DefaultSite}/saml/acs")
        }).ToActionResult();
    }

    [HttpPost("acs")]
    public async Task<IActionResult> Acs()
    {
        var binding = new Saml2PostBinding();
        var saml2AuthnResponse = new Saml2AuthnResponse(_config);

        try
        {
            var genericRequest = Request.ToGenericHttpRequest(validate: true);
            binding.ReadSamlResponse(genericRequest, saml2AuthnResponse);
            if (saml2AuthnResponse.Status != Saml2StatusCodes.Success)
            {
                return BadRequest(new { error = "SAML response status was not Success." });
            }
            binding.Unbind(genericRequest, saml2AuthnResponse);
        }
        catch (Exception)
        {
            // Generic - no internal validation detail leaked to the caller.
            return BadRequest(new { error = "Invalid SAML response." });
        }

        var principal = new ClaimsPrincipal(saml2AuthnResponse.ClaimsIdentity);

        AppUser user;
        try
        {
            user = await _provisioning.FindOrProvisionAsync(principal, "Saml");
        }
        catch (SsoProvisioningException ex)
        {
            _auditLog.Record(AuditEventTypes.LoginSamlFailure, null, null, null, metadataJson: ex.Message);
            await _db.SaveChangesAsync();
            return BadRequest(new { error = ex.Message });
        }

        return Ok(new { token = _jwtIssuer.IssueToken(user) });
    }
}
