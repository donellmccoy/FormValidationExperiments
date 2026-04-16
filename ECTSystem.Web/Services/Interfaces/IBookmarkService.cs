using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for case bookmark operations.
/// Provides methods for querying, adding, and removing user-specific case bookmarks.
/// Bookmarks allow users to flag LOD cases for quick access from the bookmarks view.
/// Maps to <c>BookmarksController</c>.
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Queries LOD cases bookmarked by the current user via OData with filtering, paging, sorting, and count.
    /// Internally retrieves the user's bookmarked case IDs and then queries the Cases entity set
    /// filtered to those IDs, combining with any additional user-supplied filter.
    /// </summary>
    /// <param name="filter">An additional OData <c>$filter</c> expression to further restrict bookmarked cases, or <c>null</c> for no extra filtering.</param>
    /// <param name="top">The maximum number of cases to return (<c>$top</c>), or <c>null</c> for the server default.</param>
    /// <param name="skip">The number of cases to skip for paging (<c>$skip</c>), or <c>null</c> for no offset.</param>
    /// <param name="orderby">An OData <c>$orderby</c> expression (e.g., <c>"CreatedDate desc"</c>), or <c>null</c> for default ordering.</param>
    /// <param name="count">If <c>true</c>, requests an inline count of total matching bookmarked cases for paging UI.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="ODataServiceResult{T}"/> containing the matching bookmarked cases and optional total count.</returns>
    Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        string? expand = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a bookmark for the given case, associating it with the currently authenticated user.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case to bookmark.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the current user's bookmark for the given case. No-op if the case is not bookmarked.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case to un-bookmark.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has bookmarked the given case.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case to check.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns><c>true</c> if the case is bookmarked by the current user; otherwise, <c>false</c>.</returns>
    Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of the supplied case IDs that the current user has bookmarked.
    /// Used for batch-checking bookmark status when rendering case list grids.
    /// </summary>
    /// <param name="caseIds">An array of LOD case primary keys to check for bookmarks.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="HashSet{T}"/> of case IDs from <paramref name="caseIds"/> that are bookmarked by the current user.</returns>
    Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries bookmarked LOD cases filtered by current workflow state via the <c>ByCurrentState</c> OData function.
    /// Used when the user filters bookmarked cases by workflow state, which is a computed property
    /// derived from <c>WorkflowStateHistories</c> and cannot be filtered via standard OData <c>$filter</c>.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesByCurrentStateAsync(
        WorkflowState[]? includeStates = null,
        WorkflowState[]? excludeStates = null,
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        string? expand = null, CancellationToken cancellationToken = default);
}
