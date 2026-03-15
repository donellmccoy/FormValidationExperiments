using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for case bookmark operations.
/// Maps to <c>CaseBookmarksController</c>.
/// </summary>
public class BookmarkHttpService : ODataServiceBase, IBookmarkService
{
    public BookmarkHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get all bookmarked case IDs for the current user.
        var bookmarkQuery = Client.For<CaseBookmark>("CaseBookmarks")
            .Select("LineOfDutyCaseId");

        var bookmarkResponse = await Client.GetAsync(bookmarkQuery, cancellationToken);

        var bookmarkedIds = bookmarkResponse.Value?.Select(b => b.LineOfDutyCaseId).ToList() ?? [];

        if (bookmarkedIds.Count == 0)
        {
            return new ODataServiceResult<LineOfDutyCase> { Value = [], Count = 0 };
        }

        // Step 2: Query Cases filtered to the bookmarked IDs.
        var idFilter = $"Id in ({string.Join(",", bookmarkedIds)})";
        var combinedFilter = string.IsNullOrEmpty(filter) ? idFilter : $"({idFilter}) and ({filter})";

        var caseQuery = Client.For<LineOfDutyCase>("Cases")
            .Filter(combinedFilter);

        if (top.HasValue) caseQuery = caseQuery.Top(top.Value);
        if (skip.HasValue) caseQuery = caseQuery.Skip(skip.Value);
        if (!string.IsNullOrEmpty(orderby)) caseQuery = caseQuery.OrderBy(orderby);
        if (count == true) caseQuery = caseQuery.Count();

        var response = await Client.GetAsync(caseQuery, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response.Value?.ToList() ?? [],
            Count = (int)(response.Count ?? 0)
        };
    }

    /// <inheritdoc />
    public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        await Client.CreateAsync("CaseBookmarks",
            new CaseBookmark { LineOfDutyCaseId = caseId }, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Client.For<CaseBookmark>("CaseBookmarks")
            .Filter($"LineOfDutyCaseId eq {caseId}")
            .Top(1)
            .Select("Id");

        var response = await Client.GetAsync(query, cancellationToken);

        var bookmarkId = response.Value?.FirstOrDefault()?.Id;

        if (bookmarkId is null or 0)
        {
            return;
        }

        await Client.DeleteAsync("CaseBookmarks", bookmarkId, null, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Client.For<CaseBookmark>("CaseBookmarks")
            .Filter($"LineOfDutyCaseId eq {caseId}")
            .Top(1)
            .Select("Id");

        var response = await Client.GetAsync(query, cancellationToken);

        return response.Value?.Any() == true;
    }

    /// <inheritdoc />
    public async Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds is { Length: 0 })
        {
            return [];
        }

        var ids = string.Join(",", caseIds);
        var query = Client.For<CaseBookmark>("CaseBookmarks")
            .Filter($"LineOfDutyCaseId in ({ids})")
            .Select("LineOfDutyCaseId");

        var response = await Client.GetAsync(query, cancellationToken);

        return response.Value?.Select(b => b.LineOfDutyCaseId).ToHashSet() ?? [];
    }
}
