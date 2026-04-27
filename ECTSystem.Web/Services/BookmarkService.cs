using System.Net.Http.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class BookmarkService : ODataServiceBase, IBookmarkService
{
    public BookmarkService(EctODataContext context, HttpClient httpClient, ILogger<BookmarkService> logger)
        : base(context, httpClient, logger) { }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        string? expand = null, CancellationToken cancellationToken = default)
    {
        var query = Context.CreateFunctionQuery<LineOfDutyCase>("Cases", "Default.Bookmarked", false);

        if (!string.IsNullOrEmpty(filter))
            query = query.AddQueryOption("$filter", filter);

        if (top.HasValue)
            query = query.AddQueryOption("$top", top.Value);

        if (skip.HasValue)
            query = query.AddQueryOption("$skip", skip.Value);

        if (!string.IsNullOrEmpty(orderby))
            query = query.AddQueryOption("$orderby", orderby);

        if (!string.IsNullOrEmpty(select))
            query = query.AddQueryOption("$select", select);

        if (!string.IsNullOrEmpty(expand))
            query = query.AddQueryOption("$expand", expand);

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(query, cancellationToken);

            return new ODataServiceResult<LineOfDutyCase>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = results,
            Count = results.Count
        };
    }

    public async Task<int> AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var body = new { caseId };

        var response = await HttpClient.PostAsJsonAsync("odata/Bookmarks/AddBookmark", body, JsonOptions, cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "POST odata/Bookmarks/AddBookmark", cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<AddBookmarkResponse>(JsonOptions, cancellationToken);

        return result?.Id ?? throw new InvalidOperationException("AddBookmark did not return a bookmark ID.");
    }

    private sealed class AddBookmarkResponse
    {
        public int Id { get; set; }
    }

    public async Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Context.Bookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
            .AddQueryOption("$top", 1)
            .AddQueryOption("$select", "Id");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);

        return bookmarks.Count > 0;
    }

    public async Task<Dictionary<int, int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds is { Length: 0 })
        {
            return [];
        }

        var ids = string.Join(",", caseIds);
        var query = Context.Bookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId in ({ids})")
            .AddQueryOption("$select", "Id,LineOfDutyCaseId");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);

        return bookmarks.ToDictionary(b => b.LineOfDutyCaseId, b => b.Id);
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesByCurrentStateAsync(
        WorkflowState[]? includeStates = null,
        WorkflowState[]? excludeStates = null,
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        string? expand = null, CancellationToken cancellationToken = default)
    {
        // Step 1: Get bookmarked case IDs via the server-side Bookmarked() function.
        var bookmarkedQuery = Context.CreateFunctionQuery<LineOfDutyCase>("Cases", "Default.Bookmarked", false)
            .AddQueryOption("$select", "Id");

        var bookmarkedCases = await ExecuteQueryAsync(bookmarkedQuery, cancellationToken);
        var bookmarkedIds = bookmarkedCases.Select(c => c.Id).ToList();

        if (bookmarkedIds.Count == 0)
        {
            return new ODataServiceResult<LineOfDutyCase> { Value = [], Count = 0 };
        }

        // Step 2: Query Cases via ByCurrentState action (POST), filtered to bookmarked IDs.
        var idFilter = $"Id in ({string.Join(",", bookmarkedIds)})";
        var combinedFilter = string.IsNullOrEmpty(filter) ? idFilter : $"({idFilter}) and ({filter})";

        var url = BuildNavigationPropertyUrl("odata/Cases/ByCurrentState", combinedFilter, top, skip, orderby, count, select, expand);

        var body = new
        {
            includeStates = includeStates ?? Array.Empty<WorkflowState>(),
            excludeStates = excludeStates ?? Array.Empty<WorkflowState>()
        };

        var response = await HttpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "POST odata/Cases/ByCurrentState (bookmarked)", cancellationToken);

        if (count == true)
        {
            var result = await response.Content.ReadFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(JsonOptions, cancellationToken);

            return new ODataServiceResult<LineOfDutyCase>
            {
                Value = result?.Value ?? [],
                Count = result?.Count ?? 0
            };
        }

        var odataResult = await response.Content.ReadFromJsonAsync<ODataResponse<LineOfDutyCase>>(JsonOptions, cancellationToken);
        var results = odataResult?.Value ?? [];

        return new ODataServiceResult<LineOfDutyCase>
        {
            Value = results,
            Count = results.Count
        };
    }

    public async Task DeleteBookmarkAsync(int caseId, int bookmarkId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bookmarkId);

        var body = new { caseId, bookmarkId };

        var response = await HttpClient.PostAsJsonAsync("odata/Bookmarks/DeleteBookmark", body, JsonOptions, cancellationToken);

        await EnsureSuccessOrThrowAsync(response, "POST odata/Bookmarks/DeleteBookmark", cancellationToken);
    }
}
