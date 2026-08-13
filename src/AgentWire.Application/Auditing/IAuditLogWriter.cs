using System;

namespace AgentWire.Application.Auditing;

/// <summary>
/// Tracks an audit entry on the current DbContext change tracker but does NOT call
/// SaveChangesAsync itself - callers invoke Record() and then their own existing
/// SaveChangesAsync(), so the audit entry lands in the same transaction as the
/// business action it documents. There is deliberately no corresponding update/delete
/// method anywhere in this interface - the audit log is append-only.
/// </summary>
public interface IAuditLogWriter
{
    void Record(
        string eventType,
        Guid? organizationId,
        Guid? actorUserId,
        string? actorEmail,
        string? targetType = null,
        string? targetId = null,
        string? metadataJson = null,
        string? ipAddress = null);
}
