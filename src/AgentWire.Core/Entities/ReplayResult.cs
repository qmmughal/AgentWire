using System;
using AgentWire.Core.Enums;

namespace AgentWire.Core.Entities
{
    public class ReplayResult : IOrganizationScoped
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }
        public Guid OriginalPacketId { get; set; }
        public Guid? NewPacketId { get; set; }
        public Guid RequestedByUserId { get; set; }
        public ReplayStatus Status { get; set; } = ReplayStatus.Pending;
        public string? ErrorMessage { get; set; }
        public string? ModelOverride { get; set; }
        public double? TemperatureOverride { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}
