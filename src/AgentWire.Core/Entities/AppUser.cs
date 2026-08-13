using System;
using AgentWire.Core.Enums;

namespace AgentWire.Core.Entities
{
    public class AppUser : IOrganizationScoped
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid OrganizationId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? PasswordHash { get; set; }
        public UserRole Role { get; set; } = UserRole.Member;
        public AuthProviderType AuthProvider { get; set; } = AuthProviderType.Local;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
    }
}
