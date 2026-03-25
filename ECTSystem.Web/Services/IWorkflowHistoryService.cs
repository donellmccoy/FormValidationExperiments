using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for workflow state history operations.
/// Provides methods for querying and recording <see cref="WorkflowStateHistory"/> entries
/// that track LOD case transitions through the workflow pipeline (e.g., Member Reports →
/// LOD Initiation → Medical Assessment → Commander Review → SJA Review → Wing CC Review).
/// Maps to <c>WorkflowStateHistoriesController</c>.
/// </summary>
public interface IWorkflowHistoryService
{
    /// <summary>
    /// Queries workflow state history entries for a case via OData with filtering, paging, sorting, and count.
    /// Returns a paged result set suitable for binding to <c>RadzenDataGrid</c>.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case.</param>
    /// <param name="filter">An OData <c>$filter</c> expression to restrict results, or <c>null</c> for no filtering.</param>
    /// <param name="top">The maximum number of history entries to return (<c>$top</c>), or <c>null</c> for the server default.</param>
    /// <param name="skip">The number of entries to skip for paging (<c>$skip</c>), or <c>null</c> for no offset.</param>
    /// <param name="orderby">An OData <c>$orderby</c> expression (e.g., <c>"CreatedDate desc"</c>), or <c>null</c> for default ordering.</param>
    /// <param name="count">If <c>true</c>, requests an inline count of total matching entries for paging UI.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="ODataServiceResult{T}"/> containing the matching history entries and optional total count.</returns>
    Task<ODataServiceResult<WorkflowStateHistory>> GetWorkflowStateHistoriesAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a single workflow state history entry to the server via OData POST.
    /// Used for recording individual state transitions or milestone events.
    /// </summary>
    /// <param name="entry">The <see cref="WorkflowStateHistory"/> entry to persist, with <c>LineOfDutyCaseId</c>, <c>WorkflowState</c>, <c>Action</c>, and <c>Status</c> populated.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The persisted <see cref="WorkflowStateHistory"/> entry with server-assigned <c>Id</c> and <c>CreatedDate</c>.</returns>
    Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple workflow state history entries sequentially via individual OData POST calls.
    /// Used during bulk state transitions that generate several history entries at once
    /// (e.g., completing one step and starting the next).
    /// </summary>
    /// <param name="entries">The list of <see cref="WorkflowStateHistory"/> entries to persist in order.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of the persisted <see cref="WorkflowStateHistory"/> entries with server-assigned IDs and timestamps.</returns>
    Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the <see cref="WorkflowStateHistory.EndDate"/> of an existing workflow state history entry
    /// via OData PATCH. Used to close out the previous workflow step when transitioning to a new state.
    /// </summary>
    /// <param name="entryId">The database primary key of the history entry to update.</param>
    /// <param name="endDate">The <see cref="DateTime"/> to set as the entry's end date.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The updated <see cref="WorkflowStateHistory"/> entry returned by the server.</returns>
    Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default);
}
