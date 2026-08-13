using System;

namespace AgentWire.Core.Entities
{
    /// <summary>
    /// Append-only. No property here is ever updated after insert - immutability is enforced
    /// by the absence of any mutation endpoint, not by a soft-delete flag.
    /// </summary>
    public class AuditLogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? OrganizationId { get; set; }
        public Guid? ActorUserId { get; set; }
        public string? ActorEmail { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? MetadataJson { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
