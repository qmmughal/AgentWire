using System;
using System.Linq;
using System.Threading.Tasks;
using AgentWire.Application.Auditing;
using AgentWire.Application.Extensions;
using AgentWire.Application.Replay;
using AgentWire.Application.Security;
using AgentWire.Core.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Core.Enums;
using AgentWire.Infrastructure.Data;
using AgentWire.Presentation.Auth;
using Microsoft.EntityFrameworkCore;

namespace AgentWire.Presentation.Endpoints;

public sealed record ReplayRequest(string? Model, double? Temperature);

public static class PacketEndpoints
{
    public static void MapPacketEndpoints(this WebApplication app)
    {
        // --- Ingestion (ApiKey auth) ---
        app.MapPost("/v1/traces", async (
            AIPacket packet,
            AgentWireDbContext db,
            IPacketScanner scanner,
            ICurrentOrgAccessor org) =>
        {
            packet.OrganizationId = org.OrganizationId;
            packet.Cost = (packet.PromptTokens * 0.000001m) + (packet.CompletionTokens * 0.000002m);

            db.AIPackets.Add(packet);
            AddFindings(db, scanner, packet);

            await db.SaveChangesAsync();

            return Results.Accepted($"/v1/packets/{packet.Id}", packet);
        }).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
        {
            AuthenticationSchemes = ApiKeyAuthenticationOptions.SchemeName
        });

        // --- Packets / analytics (Bearer, any role, org-scoped) ---
        var packets = app.MapGroup("/v1/packets").RequireAuthorization();

        packets.MapGet("", async (AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var results = await db.AIPackets
                .ForCurrentOrg(org.OrganizationId)
                .OrderByDescending(p => p.CreatedAt)
                .Take(100)
                .ToListAsync();

            return Results.Ok(results);
        });

        packets.MapGet("/{id:guid}", async (Guid id, AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var packet = await db.AIPackets.ForCurrentOrg(org.OrganizationId).FirstOrDefaultAsync(p => p.Id == id);
            return packet is null ? Results.NotFound() : Results.Ok(packet);
        });

        packets.MapGet("/{id:guid}/findings", async (Guid id, AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var exists = await db.AIPackets.ForCurrentOrg(org.OrganizationId).AnyAsync(p => p.Id == id);
            if (!exists)
            {
                return Results.NotFound();
            }

            var findings = await db.SecurityFindings
                .ForCurrentOrg(org.OrganizationId)
                .Where(f => f.AIPacketId == id)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return Results.Ok(findings);
        });

        packets.MapGet("/{id:guid}/replays", async (Guid id, AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var results = await db.ReplayResults
                .ForCurrentOrg(org.OrganizationId)
                .Where(r => r.OriginalPacketId == id)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            return Results.Ok(results);
        });

        packets.MapPost("/{id:guid}/replay", async (
            Guid id,
            ReplayRequest? request,
            AgentWireDbContext db,
            ILlmClient llmClient,
            IPacketScanner scanner,
            IAuditLogWriter auditLog,
            IConfiguration config,
            ICurrentOrgAccessor org) =>
        {
            var original = await db.AIPackets.ForCurrentOrg(org.OrganizationId).FirstOrDefaultAsync(p => p.Id == id);
            if (original is null)
            {
                return Results.NotFound();
            }

            var replayResult = new ReplayResult
            {
                OrganizationId = org.OrganizationId,
                OriginalPacketId = original.Id,
                RequestedByUserId = org.UserId ?? Guid.Empty,
                ModelOverride = request?.Model,
                TemperatureOverride = request?.Temperature
            };

            var baseUrl = config["Replay:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                replayResult.Status = ReplayStatus.Failed;
                replayResult.ErrorMessage = "No LLM provider configured. Set Replay:BaseUrl (e.g. https://api.openai.com/v1 or a local Ollama /v1 endpoint) to enable replay.";
                replayResult.CompletedAt = DateTime.UtcNow;
                db.ReplayResults.Add(replayResult);
                auditLog.Record(AuditEventTypes.ReplayFailed, org.OrganizationId, org.UserId, org.UserEmail, "AIPacket", original.Id.ToString(), replayResult.ErrorMessage);
                await db.SaveChangesAsync();
                return Results.UnprocessableEntity(new { error = replayResult.ErrorMessage, replayId = replayResult.Id });
            }

            var llmRequest = new LlmCompletionRequest(
                BaseUrl: baseUrl,
                ApiKey: config["Replay:ApiKey"],
                Model: request?.Model ?? original.ModelName,
                SystemPrompt: original.SystemPrompt,
                UserPrompt: original.UserPrompt,
                Temperature: request?.Temperature,
                TimeoutSeconds: config.GetValue<int?>("Replay:TimeoutSeconds") ?? 30);

            LlmCompletionResult completion;
            var startedAt = DateTime.UtcNow;
            try
            {
                completion = await llmClient.CompleteAsync(llmRequest, default);
            }
            catch (LlmProviderException ex)
            {
                replayResult.Status = ReplayStatus.Failed;
                replayResult.ErrorMessage = ex.Message;
                replayResult.CompletedAt = DateTime.UtcNow;
                db.ReplayResults.Add(replayResult);
                auditLog.Record(AuditEventTypes.ReplayFailed, org.OrganizationId, org.UserId, org.UserEmail, "AIPacket", original.Id.ToString(), ex.Message);
                await db.SaveChangesAsync();
                return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
            }

            var newPacket = new AIPacket
            {
                OrganizationId = org.OrganizationId,
                ReplayOfPacketId = original.Id,
                TraceId = original.TraceId,
                AgentId = original.AgentId,
                ModelProvider = original.ModelProvider,
                ModelName = llmRequest.Model,
                SystemPrompt = original.SystemPrompt,
                UserPrompt = original.UserPrompt,
                LLMResponse = completion.ResponseText,
                PromptTokens = completion.PromptTokens,
                CompletionTokens = completion.CompletionTokens,
                LatencyMs = (DateTime.UtcNow - startedAt).TotalMilliseconds,
            };
            newPacket.Cost = (newPacket.PromptTokens * 0.000001m) + (newPacket.CompletionTokens * 0.000002m);

            db.AIPackets.Add(newPacket);
            AddFindings(db, scanner, newPacket);

            replayResult.Status = ReplayStatus.Succeeded;
            replayResult.NewPacketId = newPacket.Id;
            replayResult.CompletedAt = DateTime.UtcNow;
            db.ReplayResults.Add(replayResult);

            auditLog.Record(AuditEventTypes.ReplayExecuted, org.OrganizationId, org.UserId, org.UserEmail, "AIPacket", original.Id.ToString());

            await db.SaveChangesAsync();

            return Results.Ok(new
            {
                replayId = replayResult.Id,
                newPacketId = newPacket.Id,
                response = newPacket.LLMResponse,
                promptTokens = newPacket.PromptTokens,
                completionTokens = newPacket.CompletionTokens,
                latencyMs = newPacket.LatencyMs,
                cost = newPacket.Cost
            });
        });

        // --- Security findings, org-wide (Bearer, any role) ---
        app.MapGet("/v1/security/findings", async (
            AgentWireDbContext db,
            ICurrentOrgAccessor org,
            FindingType? type,
            FindingSeverity? severity,
            int page = 1,
            int pageSize = 50) =>
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize is <= 0 or > 200 ? 50 : pageSize;

            var query = db.SecurityFindings.ForCurrentOrg(org.OrganizationId);
            if (type.HasValue)
            {
                query = query.Where(f => f.FindingType == type.Value);
            }
            if (severity.HasValue)
            {
                query = query.Where(f => f.Severity == severity.Value);
            }

            var results = await query
                .OrderByDescending(f => f.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Results.Ok(results);
        }).RequireAuthorization();

        // --- Cost analytics (Bearer, any role, org-scoped) ---
        app.MapGet("/v1/analytics/costs", async (AgentWireDbContext db, ICurrentOrgAccessor org) =>
        {
            var orgPackets = await db.AIPackets.ForCurrentOrg(org.OrganizationId).ToListAsync();
            var totalCost = orgPackets.Sum(p => p.Cost);
            var breakdown = orgPackets
                .GroupBy(p => p.ModelName)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Cost));

            return Results.Ok(new { totalCost, breakdownByModel = breakdown });
        }).RequireAuthorization();
    }

    private static void AddFindings(AgentWireDbContext db, IPacketScanner scanner, AIPacket packet)
    {
        var findings = scanner.Scan(packet.SystemPrompt, packet.UserPrompt, packet.LLMResponse);
        foreach (var finding in findings)
        {
            db.SecurityFindings.Add(new SecurityFinding
            {
                AIPacketId = packet.Id,
                OrganizationId = packet.OrganizationId,
                FindingType = finding.FindingType,
                Severity = finding.Severity,
                Location = finding.Location,
                RuleId = finding.RuleId,
                MatchedTextMasked = finding.MatchedTextMasked
            });
        }
    }
}
