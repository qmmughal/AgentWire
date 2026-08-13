using System;
using System.Linq;
using AgentWire.Core.Entities;

namespace AgentWire.Application.Extensions;

/// <summary>
/// Explicit per-call-site org scoping, not a global EF query filter. This is a
/// deliberate trade-off: a global filter with ambient scoped-service state is a
/// known source of subtle bugs across design-time/migration/background contexts.
/// The safety net for a future endpoint forgetting to call this is the
/// OrgIsolationTests suite, not the type system.
/// </summary>
public static class OrgScopeExtensions
{
    public static IQueryable<T> ForCurrentOrg<T>(this IQueryable<T> query, Guid organizationId)
        where T : IOrganizationScoped
    {
        return query.Where(x => x.OrganizationId == organizationId);
    }
}
