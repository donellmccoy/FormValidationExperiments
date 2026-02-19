using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for Line of Duty API operations.
/// </summary>
public interface IDataService
{
    /// <summary>
    /// Queries LOD cases via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single LOD case by CaseId with all navigation properties.
    /// </summary>
    Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (PUTs) a LOD case entity. Returns the saved entity.
    /// </summary>
    Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches members by name, rank, unit, or service number.
    /// </summary>
    Task<List<Member>> SearchMembersAsync(string searchText, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries bookmarked cases via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<CaseBookmark>> GetBookmarkedCasesAsync(
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
}
