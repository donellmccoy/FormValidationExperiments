using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD case CRUD operations.
/// Maps to <c>CasesController</c>.
/// </summary>
public interface ICaseService
{
    /// <summary>
    /// Queries LOD cases via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single LOD case by CaseId with all navigation properties.
    /// </summary>
    Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (POST or PATCH) a LOD case entity. Returns the saved entity.
    /// </summary>
    Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically transitions a LOD case to a new workflow state and persists the
    /// associated history entries in a single server-side transaction.
    /// Returns the updated case and server-persisted history entries.
    /// </summary>
    Task<CaseTransitionResponse> TransitionCaseAsync(int caseId, CaseTransitionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks out a LOD case so other users see it as read-only.
    /// </summary>
    Task<bool> CheckOutCaseAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks in a LOD case so it becomes available for editing.
    /// </summary>
    Task<bool> CheckInCaseAsync(int caseId, CancellationToken cancellationToken = default);
}
