using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AgentWire.Infrastructure.Auth;

/// <summary>
/// Finds or creates the local AppUser for an externally-authenticated (OIDC/SAML)
/// identity. Some IdPs put the email in NameID (format=email); others use a long
/// attribute URN - checked in order, throws if neither is present rather than
/// guessing. This is a known interop rough edge inherent to not being tested
/// against a live third-party IdP.
/// </summary>
public sealed class UserProvisioningService : IUserProvisioningService
{
    private static readonly string[] EmailClaimTypes =
    [
        ClaimTypes.Email,
        "email",
        "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
    ];

    private readonly AgentWireDbContext _db;
    private readonly IConfiguration _config;
    private readonly IAuditLogWriter _auditLog;

    public UserProvisioningService(AgentWireDbContext db, IConfiguration config, IAuditLogWriter auditLog)
    {
        _db = db;
        _config = config;
        _auditLog = auditLog;
    }

    public async Task<AppUser> FindOrProvisionAsync(ClaimsPrincipal externalPrincipal, string provider)
    {
        var email = ExtractEmail(externalPrincipal);
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new SsoProvisioningException(
                $"No email claim found on the {provider} identity (checked NameID and common attribute URNs).");
        }

        var existing = await _db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);
        if (existing is not null)
        {
            existing.LastLoginAt = DateTime.UtcNow;
            _auditLog.Record(
                provider == "Saml" ? AuditEventTypes.LoginSamlSuccess : AuditEventTypes.LoginOidcSuccess,
                existing.OrganizationId, existing.Id, existing.Email);
            await _db.SaveChangesAsync();
            return existing;
        }

        var defaultOrgIdRaw = _config["Sso:DefaultOrganizationId"];
        Guid? defaultOrgId = Guid.TryParse(defaultOrgIdRaw, out var parsed) ? parsed : null;

        var org = defaultOrgId.HasValue
            ? await _db.Organizations.FirstOrDefaultAsync(o => o.Id == defaultOrgId.Value)
            : await _db.Organizations.FirstOrDefaultAsync();

        if (org is null)
        {
            throw new SsoProvisioningException(
                "No organization is configured to receive SSO-provisioned users. " +
                "Run POST /v1/setup first, or set Sso:DefaultOrganizationId.");
        }

        var displayName = externalPrincipal.FindFirst(ClaimTypes.Name)?.Value
            ?? externalPrincipal.Identity?.Name
            ?? email;

        var user = new AppUser
        {
            OrganizationId = org.Id,
            Email = email,
            DisplayName = displayName,
            PasswordHash = null,
            Role = UserRole.Member,
            AuthProvider = provider == "Saml" ? AuthProviderType.Saml : AuthProviderType.Oidc,
            LastLoginAt = DateTime.UtcNow
        };

        _db.AppUsers.Add(user);
        _auditLog.Record(AuditEventTypes.UserCreated, org.Id, user.Id, user.Email, targetType: "AppUser", targetId: user.Id.ToString());
        _auditLog.Record(
            provider == "Saml" ? AuditEventTypes.LoginSamlSuccess : AuditEventTypes.LoginOidcSuccess,
            org.Id, user.Id, user.Email);

        await _db.SaveChangesAsync();
        return user;
    }

    private static string? ExtractEmail(ClaimsPrincipal principal)
    {
        var nameId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? principal.FindFirst("nameid")?.Value;
        if (!string.IsNullOrWhiteSpace(nameId) && nameId.Contains('@'))
        {
            return nameId;
        }

        foreach (var claimType in EmailClaimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
