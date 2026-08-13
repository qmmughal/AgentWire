using System;
using System.Linq;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Presentation.Endpoints;

public sealed record SetupRequest(string OrganizationName, string AdminEmail, string AdminPassword);

public static class SetupEndpoints
{
    public static void MapSetupEndpoints(this WebApplication app)
    {
        app.MapPost("/v1/setup", async (
            SetupRequest request,
            AgentWireDbContext db,
            IJwtIssuer jwtIssuer,
            IAuditLogWriter auditLog) =>
        {
            if (await db.Organizations.AnyAsync())
            {
                return Results.Conflict(new { error = "AgentWire is already set up. Use POST /v1/auth/login." });
            }

            if (string.IsNullOrWhiteSpace(request.OrganizationName) ||
                string.IsNullOrWhiteSpace(request.AdminEmail) ||
                string.IsNullOrWhiteSpace(request.AdminPassword) ||
                request.AdminPassword.Length < 8)
            {
                return Results.BadRequest(new { error = "organizationName, adminEmail, and adminPassword (min 8 chars) are required." });
            }

            var org = new Organization { Name = request.OrganizationName, Slug = Slugify(request.OrganizationName) };
            var admin = new AppUser
            {
                OrganizationId = org.Id,
                Email = request.AdminEmail.Trim().ToLowerInvariant(),
                DisplayName = request.AdminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
                Role = UserRole.Admin,
                AuthProvider = AuthProviderType.Local
            };

            var (rawKey, prefix) = ApiKeyHasher.GenerateRawKey();
            var apiKey = new ApiKey
            {
                OrganizationId = org.Id,
                Name = "Default",
                KeyHash = ApiKeyHasher.Hash(rawKey),
                KeyPrefix = prefix,
                CreatedByUserId = admin.Id
            };

            db.Organizations.Add(org);
            db.AppUsers.Add(admin);
            db.ApiKeys.Add(apiKey);

            auditLog.Record(AuditEventTypes.OrgCreated, org.Id, admin.Id, admin.Email, "Organization", org.Id.ToString());
            auditLog.Record(AuditEventTypes.UserCreated, org.Id, admin.Id, admin.Email, "AppUser", admin.Id.ToString());
            auditLog.Record(AuditEventTypes.ApiKeyCreated, org.Id, admin.Id, admin.Email, "ApiKey", apiKey.Id.ToString());

            try
            {
                await db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Results.Conflict(new { error = "AgentWire is already set up." });
            }

            var jwt = jwtIssuer.IssueToken(admin);

            return Results.Ok(new
            {
                organizationId = org.Id,
                adminUserId = admin.Id,
                adminEmail = admin.Email,
                apiKey = rawKey,
                jwt
            });
        });
    }

    private static string Slugify(string name)
    {
        var slug = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        slug = slug.Trim('-');
        if (string.IsNullOrEmpty(slug))
        {
            slug = "org";
        }
        return slug + "-" + Guid.NewGuid().ToString("N")[..6];
    }
}
