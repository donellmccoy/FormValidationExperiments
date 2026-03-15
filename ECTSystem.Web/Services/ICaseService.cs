using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD case CRUD operations.
/// Provides methods for querying, saving, and managing the lifecycle of
/// <see cref="LineOfDutyCase"/> entities via OData. Maps to <c>CasesController</c>.
/// </summary>
public interface ICaseService
{
    /// <summary>
    /// Queries LOD cases via OData with filtering, paging, sorting, and count.
    /// Returns a paged result set suitable for binding to <c>RadzenDataGrid</c>.
    /// </summary>
    /// <param name="filter">An OData <c>$filter</c> expression to restrict results (e.g., <c>"Status eq 'Open'"</c>), or <c>null</c> for no filtering.</param>
    /// <param name="top">The maximum number of cases to return (<c>$top</c>), or <c>null</c> for the server default.</param>
    /// <param name="skip">The number of cases to skip for paging (<c>$skip</c>), or <c>null</c> for no offset.</param>
    /// <param name="orderby">An OData <c>$orderby</c> expression (e.g., <c>"CreatedDate desc"</c>), or <c>null</c> for default ordering.</param>
    /// <param name="select">An OData <c>$select</c> expression to limit returned properties, or <c>null</c> to return all properties.</param>
    /// <param name="count">If <c>true</c>, requests an inline count of total matching records for paging UI.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="ODataServiceResult{T}"/> containing the matching cases and optional total count.</returns>
    Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single LOD case by its human-readable <c>CaseId</c> string with all
    /// navigation properties expanded (Authorities, Appeals, Member, MEDCON, INCAP,
    /// Notifications, WorkflowStateHistories).
    /// </summary>
    /// <param name="caseId">The human-readable case identifier string (e.g., <c>"LOD-2025-00042"</c>).</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The fully-loaded <see cref="LineOfDutyCase"/> if found; otherwise, <c>null</c>.</returns>
    Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a LOD case entity. Creates a new case via POST if <see cref="LineOfDutyCase.Id"/>
    /// is <c>0</c>; otherwise, updates the existing case via PATCH using only scalar properties
    /// to avoid OData <c>Delta&lt;T&gt;</c> binding failures with navigation properties.
    /// </summary>
    /// <param name="lodCase">The case entity to create or update.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The saved <see cref="LineOfDutyCase"/> entity as returned by the server, including any server-generated values.</returns>
    Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions a LOD case to a new workflow state and persists the
    /// associated history entries in a single server-side transaction.
    /// Returns the updated case and server-persisted history entries.
    /// </summary>
    /// <param name="caseId">The database primary key of the case to transition.</param>
    /// <param name="request">The transition request containing the target workflow state and history entries to persist.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="CaseTransitionResponse"/> containing the updated case and the server-assigned history entries.</returns>
    Task<CaseTransitionResponse> TransitionCaseAsync(int caseId, CaseTransitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out a LOD case by setting <c>IsCheckedOut</c> to <c>true</c>,
    /// signaling to other users that the case is currently being edited and should be treated as read-only.
    /// </summary>
    /// <param name="caseId">The database primary key of the case to check out.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the checkout succeeded; <c>false</c> if the server rejected the request.</returns>
    Task<bool> CheckOutCaseAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks in a LOD case by setting <c>IsCheckedOut</c> to <c>false</c>,
    /// releasing the editing lock so the case becomes available for other users to edit.
    /// </summary>
    /// <param name="caseId">The database primary key of the case to check in.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the check-in succeeded; <c>false</c> if the server rejected the request.</returns>
    Task<bool> CheckInCaseAsync(int caseId, CancellationToken cancellationToken = default);
}
