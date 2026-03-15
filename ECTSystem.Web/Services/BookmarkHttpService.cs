using System.Net.Http.Json;
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
        var bookmarkResponse = await HttpClient.GetFromJsonAsync<ODataResponse<CaseBookmark>>(
            "odata/CaseBookmarks?$select=LineOfDutyCaseId", ODataJsonOptions, cancellationToken);

        var bookmarkedIds = bookmarkResponse?.Value.Select(b => b.LineOfDutyCaseId).ToList() ?? [];

        if (bookmarkedIds.Count == 0)
        {
            return new ODataServiceResult<LineOfDutyCase> { Value = [], Count = 0 };
        }

        // Step 2: Query Cases filtered to the bookmarked IDs.
        var idFilter = $"Id in ({string.Join(",", bookmarkedIds)})";
        var combinedFilter = string.IsNullOrEmpty(filter) ? idFilter : $"({idFilter}) and ({filter})";

        var parts = new List<string> { $"$filter={combinedFilter}" };

        if (top.HasValue)
        {
            parts.Add($"$top={top.Value}");
        }

        if (skip.HasValue)
        {
            parts.Add($"$skip={skip.Value}");
        }

        if (!string.IsNullOrEmpty(orderby))
        {
            parts.Add($"$orderby={orderby}");
        }

        if (count == true)
        {
            parts.Add("$count=true");
        }

        var url = $"odata/Cases?{string.Join("&", parts)}";
        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(
            url, ODataJsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = response?.Value ?? [],
            Count = response?.Count ?? 0
        };
    }

    /// <inheritdoc />
    public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var response = await HttpClient.PostAsJsonAsync("odata/CaseBookmarks", new { LineOfDutyCaseId = caseId }, ODataJsonOptions, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var bookmarks = await HttpClient.GetFromJsonAsync<ODataResponse<CaseBookmark>>(
            $"odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {caseId}&$top=1&$select=Id",
            ODataJsonOptions, cancellationToken);

        var bookmarkId = bookmarks?.Value?.FirstOrDefault()?.Id;

        if (bookmarkId is null or 0)
        {
            return;
        }

        var response = await HttpClient.DeleteAsync($"odata/CaseBookmarks({bookmarkId})", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var bookmarks = await HttpClient.GetFromJsonAsync<ODataResponse<CaseBookmark>>(
            $"odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {caseId}&$top=1&$select=Id",
            ODataJsonOptions, cancellationToken);

        return bookmarks?.Value is { Count: > 0 };
    }

    /// <inheritdoc />
    public async Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds is { Length: 0 })
        {
            return [];
        }

        var ids = string.Join(",", caseIds);
        var response = await HttpClient.GetFromJsonAsync<ODataResponse<CaseBookmark>>(
            $"odata/CaseBookmarks?$filter=LineOfDutyCaseId in ({ids})&$select=LineOfDutyCaseId",
            ODataJsonOptions, cancellationToken);

        return response?.Value?.Select(b => b.LineOfDutyCaseId).ToHashSet() ?? [];
    }
}
