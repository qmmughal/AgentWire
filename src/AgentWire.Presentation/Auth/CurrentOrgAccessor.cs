using System;
using System.Security.Claims;
using AgentWire.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace AgentWire.Presentation.Auth;

public interface ICurrentOrgAccessor
{
    Guid OrganizationId { get; }
    Guid? UserId { get; }
    string? UserEmail { get; }
    string? Role { get; }
}

/// <summary>
/// Reads org_id from the current principal - works identically whether the caller
/// authenticated via Bearer (JWT) or ApiKey, since both schemes populate org_id.
/// </summary>
public sealed class CurrentOrgAccessor : ICurrentOrgAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentOrgAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid OrganizationId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst(JwtIssuer.OrgClaimType)?.Value;
            if (value is null || !Guid.TryParse(value, out var orgId))
            {
                throw new InvalidOperationException("No org_id claim present on the current principal.");
            }
            return orgId;
        }
    }

    public Guid? UserId
    {
        get
        {
            var value = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? UserEmail =>
        _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Email)?.Value;

    public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(JwtIssuer.RoleClaimType)?.Value;
}
