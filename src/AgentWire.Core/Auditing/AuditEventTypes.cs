namespace AgentWire.Core.Auditing;

public static class AuditEventTypes
{
    public const string LoginLocalSuccess = "auth.login.local.success";
    public const string LoginLocalFailure = "auth.login.local.failure";
    public const string LoginOidcSuccess = "auth.login.oidc.success";
    public const string LoginOidcFailure = "auth.login.oidc.failure";
    public const string LoginSamlSuccess = "auth.login.saml.success";
    public const string LoginSamlFailure = "auth.login.saml.failure";
    public const string UserCreated = "user.created";
    public const string UserRoleChanged = "user.role_changed";
    public const string ApiKeyCreated = "apikey.created";
    public const string ApiKeyRevoked = "apikey.revoked";
    public const string ReplayExecuted = "replay.executed";
    public const string ReplayFailed = "replay.failed";
    public const string OrgCreated = "org.created";
}
