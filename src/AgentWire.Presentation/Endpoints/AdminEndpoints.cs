using System;
using System.Linq;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Auth;
using AgentWire.Application.Extensions;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Data;
using AgentWire.Presentation.Auth;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Presentation.Endpoints;

public sealed record CreateUserRequest(string Email, string TempPassword, UserRole Role);
public sealed record UpdateUserRoleRequest(UserRole Role);
public sealed record CreateApiKeyRequest(string Name);

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("").RequireAuthorization("AdminOnly");

        // --- Users ---
        admin.MapGet("/v1/users", async (AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var users = await db.AppUsers
                .ForCurrentOrg(org.OrganizationId)
                .OrderBy(u => u.CreatedAt)
                .Select(u => new { u.Id, u.Email, u.DisplayName, u.Role, u.AuthProvider, u.CreatedAt, u.LastLoginAt })
                .ToListAsync();
            return Results.Ok(users);
        });

        admin.MapPost("/v1/users", async (
            CreateUserRequest request,
            AgentWireDbContext db,
            IAuditLogWriter auditLog,
            ICurrentOrgAccessor org) =>
        {
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.TempPassword) || request.TempPassword.Length < 8)
            {
                return Results.BadRequest(new { error = "email and tempPassword (min 8 chars) are required." });
            }

            var email = request.Email.Trim().ToLowerInvariant();
            if (await db.AppUsers.AnyAsync(u => u.Email == email))
            {
                return Results.Conflict(new { error = "A user with that email already exists." });
            }

            var user = new AppUser
            {
                OrganizationId = org.OrganizationId,
                Email = email,
                DisplayName = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.TempPassword),
                Role = request.Role,
                AuthProvider = AuthProviderType.Local
            };

            db.AppUsers.Add(user);
            auditLog.Record(AuditEventTypes.UserCreated, org.OrganizationId, org.UserId, org.UserEmail, "AppUser", user.Id.ToString());
            await db.SaveChangesAsync();

            return Results.Created($"/v1/users/{user.Id}", new { user.Id, tempPassword = request.TempPassword });
        });

        admin.MapPatch("/v1/users/{id:guid}/role", async (
            Guid id,
            UpdateUserRoleRequest request,
            AgentWireDbContext db,
            IAuditLogWriter auditLog,
            ICurrentOrgAccessor org) =>
        {
            var user = await db.AppUsers.ForCurrentOrg(org.OrganizationId).FirstOrDefaultAsync(u => u.Id == id);
            if (user is null)
            {
                return Results.NotFound();
            }

            user.Role = request.Role;
            auditLog.Record(AuditEventTypes.UserRoleChanged, org.OrganizationId, org.UserId, org.UserEmail, "AppUser", user.Id.ToString(), $"{{\"newRole\":\"{request.Role}\"}}");
            await db.SaveChangesAsync();

            return Results.Ok(new { user.Id, user.Role });
        });

        // --- API keys ---
        admin.MapGet("/v1/apikeys", async (AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var keys = await db.ApiKeys
                .ForCurrentOrg(org.OrganizationId)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new { k.Id, k.Name, k.KeyPrefix, k.CreatedAt, k.RevokedAt, k.LastUsedAt })
                .ToListAsync();
            return Results.Ok(keys);
        });

        admin.MapPost("/v1/apikeys", async (
            CreateApiKeyRequest request,
            AgentWireDbContext db,
            IAuditLogWriter auditLog,
            ICurrentOrgAccessor org) =>
        {
            var (rawKey, prefix) = ApiKeyHasher.GenerateRawKey();
            var apiKey = new ApiKey
            {
                OrganizationId = org.OrganizationId,
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Unnamed" : request.Name,
                KeyHash = ApiKeyHasher.Hash(rawKey),
                KeyPrefix = prefix,
                CreatedByUserId = org.UserId
            };

            db.ApiKeys.Add(apiKey);
            auditLog.Record(AuditEventTypes.ApiKeyCreated, org.OrganizationId, org.UserId, org.UserEmail, "ApiKey", apiKey.Id.ToString());
            await db.SaveChangesAsync();

            return Results.Created($"/v1/apikeys/{apiKey.Id}", new { apiKey.Id, key = rawKey });
        });

        admin.MapPost("/v1/apikeys/{id:guid}/revoke", async (
            Guid id,
            AgentWireDbContext db,
            IAuditLogWriter auditLog,
            ICurrentOrgAccessor org) =>
        {
            var apiKey = await db.ApiKeys.ForCurrentOrg(org.OrganizationId).FirstOrDefaultAsync(k => k.Id == id);
            if (apiKey is null)
            {
                return Results.NotFound();
            }

            apiKey.RevokedAt = DateTime.UtcNow;
            auditLog.Record(AuditEventTypes.ApiKeyRevoked, org.OrganizationId, org.UserId, org.UserEmail, "ApiKey", apiKey.Id.ToString());
            await db.SaveChangesAsync();

            return Results.Ok(new { apiKey.Id, apiKey.RevokedAt });
        });

        // --- Audit log (read-only, Admin-only, org-scoped; no mutation route exists anywhere) ---
        admin.MapGet("/v1/audit-log", async (
            AgentWireDbContext db,
            ICurrentOrgAccessor org,
            string? eventType,
            int page = 1,
            int pageSize = 50) =>
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

            var query = db.AuditLogEntries.Where(e => e.OrganizationId == org.OrganizationId);
            if (!string.IsNullOrWhiteSpace(eventType))
            {
                query = query.Where(e => e.EventType == eventType);
            }

            var results = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(results);
        });
    }
}
