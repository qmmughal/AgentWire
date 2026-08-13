using System;

namespace AgentWire.Core.Entities
{
    public interface IOrganizationScoped
    {
        Guid OrganizationId { get; }
    }
}
