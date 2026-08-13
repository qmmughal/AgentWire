using AgentWire.Core.Entities;

namespace AgentWire.Application.Auth;

public interface IJwtIssuer
{
    string IssueToken(AppUser user);
}
