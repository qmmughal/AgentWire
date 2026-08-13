using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using AgentWire.Application.Auth;
using AgentWire.Infrastructure.Auth;
using AgentWire.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgentWire.Presentation.Auth;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-API-Key";
}

/// <summary>
/// Reads X-API-Key, hashes and looks it up, and populates a principal with only an
/// org_id claim. Used exclusively by POST /v1/traces (agent/SDK ingestion) - never
/// for the human-facing dashboard/API endpoints, which use JWT Bearer instead.
/// </summary>
public sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    private readonly AgentWireDbContext _db;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        AgentWireDbContext db)
        : base(options, logger, encoder)
    {
        _db = db;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyAuthenticationOptions.HeaderName, out var headerValue))
        {
            return AuthenticateResult.Fail("Missing X-API-Key header.");
        }

        var rawKey = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.Fail("Empty X-API-Key header.");
        }

        var keyHash = ApiKeyHasher.Hash(rawKey);
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash);

        if (apiKey is null || apiKey.RevokedAt is not null)
        {
            return AuthenticateResult.Fail("Invalid or revoked API key.");
        }

        apiKey.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var claims = new[]
        {
            new Claim(JwtIssuer.OrgClaimType, apiKey.OrganizationId.ToString()),
            new Claim("api_key_id", apiKey.Id.ToString()),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }
}
