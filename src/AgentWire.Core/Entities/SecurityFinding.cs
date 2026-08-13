using System;
using AgentWire.Core.Enums;

namespace AgentWire.Core.Entities
{
    public class SecurityFinding : IOrganizationScoped
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AIPacketId { get; set; }
        public Guid OrganizationId { get; set; }
        public FindingType FindingType { get; set; }
        public FindingSeverity Severity { get; set; }
        public FindingLocation Location { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public string MatchedTextMasked { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
