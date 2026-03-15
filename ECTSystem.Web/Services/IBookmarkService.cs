using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for case bookmark operations.
/// Maps to <c>CaseBookmarksController</c>.
/// </summary>
public interface IBookmarkService
{
    /// <summary>
    /// Queries cases bookmarked by the current user via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a bookmark for the given case.
    /// </summary>
    Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes any bookmark for the given case.
    /// </summary>
    Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks whether the current user has bookmarked the given case.
    /// </summary>
    Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the set of case IDs (from the supplied list) that the current user has bookmarked.
    /// </summary>
    Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default);
}
