using System;
using AgentWire.Application.Auditing;
using AgentWire.Core.Entities;
using AgentWire.Infrastructure.Data;

namespace AgentWire.Infrastructure.Auditing;

public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly AgentWireDbContext _db;

    public AuditLogWriter(AgentWireDbContext db)
    {
        _db = db;
    }

    public void Record(
        string eventType,
        Guid? organizationId,
        Guid? actorUserId,
        string? actorEmail,
        string? targetType = null,
        string? targetId = null,
        string? metadataJson = null,
        string? ipAddress = null)
    {
        _db.AuditLogEntries.Add(new AuditLogEntry
        {
            EventType = eventType,
            OrganizationId = organizationId,
            ActorUserId = actorUserId,
            ActorEmail = actorEmail,
            TargetType = targetType,
            TargetId = targetId,
            MetadataJson = metadataJson,
            IpAddress = ipAddress,
            CreatedAt = DateTime.UtcNow
        });
    }
}
