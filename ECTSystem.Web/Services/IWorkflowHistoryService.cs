using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for workflow state history operations.
/// Maps to <c>WorkflowStateHistoriesController</c>.
/// </summary>
public interface IWorkflowHistoryService
{
    /// <summary>
    /// Queries workflow state histories for a case via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single workflow state history entry.
    /// </summary>
    Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple workflow state history entries sequentially.
    /// </summary>
    Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default);
}
