using System;

namespace AgentWire.Core.Entities
{
    public class ApiKey : IOrganizationScoped
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KeyHash { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public Guid? CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
