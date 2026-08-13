using System;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Infrastructure.Data;
using AgentWire.Presentation.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Presentation.Endpoints;

public sealed record LoginRequest(string Email, string Password);

public static class AuthEndpoints
{
    public const string OidcSchemeName = "Oidc";
    public const string CookieSsoSchemeName = "CookieSso";

    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/auth/login", async (
            LoginRequest request,
            AgentWireDbContext db,
            IJwtIssuer jwtIssuer,
            IAuditLogWriter auditLog) =>
        {
            var email = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Email == email);

            var passwordOk = user?.PasswordHash is not null
                && BCrypt.Net.BCrypt.Verify(request.Password ?? string.Empty, user.PasswordHash);

            if (!passwordOk)
            {
                // Deliberately generic - same response and same "failure" audit event for both
                // wrong-password and unknown-email, so this endpoint can't be used to enumerate users.
                auditLog.Record(AuditEventTypes.LoginLocalFailure, user?.OrganizationId, user?.Id, email);
                await db.SaveChangesAsync();
                return Results.Unauthorized();
            }

            user!.LastLoginAt = DateTime.UtcNow;
            auditLog.Record(AuditEventTypes.LoginLocalSuccess, user.OrganizationId, user.Id, user.Email);
            await db.SaveChangesAsync();

            return Results.Ok(new { token = jwtIssuer.IssueToken(user) });
        });

        app.MapGet("/v1/auth/oidc/login", (IConfiguration config) =>
        {
            if (!config.GetValue<bool>("Oidc:Enabled"))
            {
                return Results.Problem("OIDC SSO is not configured on this instance.", statusCode: StatusCodes.Status404NotFound);
            }

            var props = new AuthenticationProperties { RedirectUri = "/v1/auth/oidc/complete" };
            return Results.Challenge(props, [OidcSchemeName]);
        });

        app.MapGet("/v1/auth/oidc/complete", async (
            HttpContext ctx,
            IUserProvisioningService provisioning,
            IJwtIssuer jwtIssuer,
            IAuditLogWriter auditLog,
            AgentWireDbContext db) =>
        {
            var result = await ctx.AuthenticateAsync(CookieSsoSchemeName);
            if (!result.Succeeded || result.Principal is null)
            {
                return Results.Unauthorized();
            }

            AppUser user;
            try
            {
                user = await provisioning.FindOrProvisionAsync(result.Principal, "Oidc");
            }
            catch (SsoProvisioningException ex)
            {
                auditLog.Record(AuditEventTypes.LoginOidcFailure, null, null, null, metadataJson: ex.Message);
                await db.SaveChangesAsync();
                return Results.BadRequest(new { error = ex.Message });
            }

            await ctx.SignOutAsync(CookieSsoSchemeName);
            return Results.Ok(new { token = jwtIssuer.IssueToken(user) });
        });

        app.MapGet("/v1/auth/me", (ICurrentOrgAccessor org) => Results.Ok(new
        {
            userId = org.UserId,
            email = org.UserEmail,
            organizationId = org.OrganizationId,
            role = org.Role
        })).RequireAuthorization();
    }
}
