using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Extensions;

public static class LineOfDutyCaseQueryExtensions
{
    /// <summary>
    /// Returns a queryable for <see cref="LineOfDutyCase"/> with all navigation properties eagerly loaded.
    /// Uses <c>AsSplitQuery</c> to avoid cartesian-product SQL when multiple collections are included.
    /// </summary>
    /// <param name="query">The queryable to extend.</param>
    /// <returns>An <see cref="IQueryable{T}"/> with full includes applied.</returns>
    public static IQueryable<LineOfDutyCase> IncludeAllNavigations(this IQueryable<LineOfDutyCase> query)
    {
        return query
            .AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Authorities)
            .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.Member)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .Include(c => c.Notifications)
            .Include(c => c.WorkflowStateHistories);
    }
}