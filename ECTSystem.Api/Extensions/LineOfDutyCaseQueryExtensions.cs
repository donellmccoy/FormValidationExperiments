using Microsoft.EntityFrameworkCore;
using ECTSystem.Shared.Enums;
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
            .Include(c => c.WorkflowStateHistories)
            .Include(c => c.WitnessStatements)
            .Include(c => c.AuditComments);
    }

    /// <summary>
    /// Filters cases whose current workflow state (the most recent <see cref="WorkflowStateHistory"/>
    /// entry by <c>CreatedDate</c> then <c>Id</c>) is one of the specified <paramref name="states"/>.
    /// Translates to a SQL subquery and is fully composable with additional OData query options.
    /// </summary>
    public static IQueryable<LineOfDutyCase> WhereCurrentWorkflowStateIn(
        this IQueryable<LineOfDutyCase> query,
        params WorkflowState[] states)
    {
        return query.Where(c => states.Contains(
            c.WorkflowStateHistories
                .OrderByDescending(h => h.CreatedDate)
                .ThenByDescending(h => h.Id)
                .Select(h => h.WorkflowState)
                .FirstOrDefault()));
    }

    /// <summary>
    /// Filters cases whose current workflow state (the most recent <see cref="WorkflowStateHistory"/>
    /// entry by <c>CreatedDate</c> then <c>Id</c>) is NOT one of the specified <paramref name="states"/>.
    /// Translates to a SQL subquery and is fully composable with additional OData query options.
    /// </summary>
    public static IQueryable<LineOfDutyCase> WhereCurrentWorkflowStateNotIn(
        this IQueryable<LineOfDutyCase> query,
        params WorkflowState[] states)
    {
        return query.Where(c => !states.Contains(
            c.WorkflowStateHistories
                .OrderByDescending(h => h.CreatedDate)
                .ThenByDescending(h => h.Id)
                .Select(h => h.WorkflowState)
                .FirstOrDefault()));
    }
}