using System.Net.Http.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class BookmarkService : ODataServiceBase, IBookmarkService
{
    public BookmarkService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        CancellationToken cancellationToken = default)
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

    public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var dto = new CreateBookmarkDto { LineOfDutyCaseId = caseId };
        var response = await HttpClient.PostAsJsonAsync("odata/Bookmarks", dto, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Context.Bookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
            .AddQueryOption("$top", 1)
            .AddQueryOption("$select", "Id");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);
        var bookmark = bookmarks.FirstOrDefault();

        if (bookmark is null || bookmark.Id == 0)
        {
            return;
        }

        if (Context.GetEntityDescriptor(bookmark) != null)
        {
            Context.Detach(bookmark);
        }

        var response = await HttpClient.DeleteAsync($"odata/Bookmarks({bookmark.Id})", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
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

    public async Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken cancellationToken = default)
    {
        if (caseIds is { Length: 0 })
        {
            return [];
        }

        var ids = string.Join(",", caseIds);
        var query = Context.Bookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId in ({ids})")
            .AddQueryOption("$select", "LineOfDutyCaseId");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);

        return bookmarks.Select(b => b.LineOfDutyCaseId).ToHashSet();
    }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesByCurrentStateAsync(
        WorkflowState[]? includeStates = null,
        WorkflowState[]? excludeStates = null,
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, string? select = null, bool? count = null,
        CancellationToken cancellationToken = default)
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

        var url = BuildNavigationPropertyUrl("odata/Cases/ByCurrentState", combinedFilter, top, skip, orderby, count, select);

        var body = new
        {
            includeStates = includeStates ?? Array.Empty<WorkflowState>(),
            excludeStates = excludeStates ?? Array.Empty<WorkflowState>()
        };

        var response = await HttpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();

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
}
