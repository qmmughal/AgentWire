using System.Security.Claims;
using System.Threading.Tasks;
using AgentWire.Core.Entities;

namespace AgentWire.Application.Auth;

/// <summary>
/// External IdPs (OIDC/SAML) authenticate identity only. This service finds-or-creates
/// the corresponding local AppUser, always as Member in the configured default
/// organization - SSO never auto-grants Admin, only bootstrap or an existing Admin can.
/// </summary>
public interface IUserProvisioningService
{
    Task<AppUser> FindOrProvisionAsync(ClaimsPrincipal externalPrincipal, string provider);
}

public sealed class SsoProvisioningException : System.Exception
{
    public SsoProvisioningException(string message) : base(message)
    {
    }
}
