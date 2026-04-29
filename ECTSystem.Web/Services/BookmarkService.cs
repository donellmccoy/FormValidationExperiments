using System.Net.Http.Json;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData client for the per-user bookmarks collection and the bookmark-scoped case projections.
/// </summary>
/// <remarks>
/// <para>
/// Reads use the server-bound function <c>Default.Bookmarked</c> on the <c>Cases</c> set so the
/// "current user" filter is applied server-side from the bearer token rather than being trusted
/// from the client. Writes go through bound actions (<c>AddBookmark</c>, <c>DeleteBookmark</c>) so
/// the body schema is enforced by the OData metadata.
/// </para>
/// <para>
/// <b>Single-round-trip <see cref="GetBookmarkedCasesByCurrentStateAsync"/></b> — POSTs to the
/// bound action <c>Cases/BookmarkedByCurrentState</c>, which composes the bookmark filter for the
/// authenticated user with the include/exclude workflow-state filter on the server side in a
/// single LINQ query. Standard OData query options (<c>$filter</c>, <c>$orderby</c>, <c>$top</c>,
/// <c>$skip</c>, <c>$count</c>, <c>$select</c>, <c>$expand</c>) compose on top via the URL.
/// </para>
/// <para>
/// <b><see cref="IsBookmarkedAsync"/> standalone vs. piggybacked</b> — <see cref="CaseService.GetCaseAsync"/>
/// already reads the <c>X-Case-IsBookmarked</c> response header so callers that loaded the case do
/// not need to call this method. <see cref="IsBookmarkedAsync"/> exists for callers that need the
/// answer without loading the case; it could be tightened to <c>$top=0&amp;$count=true</c> instead of
/// fetching a row, but the current <c>$top=1&amp;$select=Id</c> is already a single-column 1-row read.
/// </para>
/// <para>
/// <b>Mutation count side-effects</b> — Add/Delete intentionally do <i>not</i> touch
/// <see cref="BookmarkCountService"/>. Callers (pages) invoke <c>Increment()</c>/<c>Decrement()</c>
/// only after the server call succeeds so the badge is consistent with the server state. See
/// the §3.13 deferred follow-up for moving that orchestration into this service once the count
/// service is rewired.
/// </para>
/// </remarks>
public class BookmarkService : ODataServiceBase, IBookmarkService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookmarkService"/> class.
    /// </summary>
    /// <param name="context">The OData client context for query composition.</param>
    /// <param name="httpClient">The named <c>OData</c> <see cref="HttpClient"/> for bound action POSTs.</param>
    /// <param name="logger">The logger for diagnostic events.</param>
    public BookmarkService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<BookmarkService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

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
        // Single round-trip: server composes (bookmark filter for current user) AND (current-state filter).
        var url = BuildNavigationPropertyUrl("odata/Cases/BookmarkedByCurrentState", filter, top, skip, orderby, count, select, expand);

        var body = new
        {
            includeStates = includeStates ?? Array.Empty<WorkflowState>(),
            excludeStates = excludeStates ?? Array.Empty<WorkflowState>()
        };

        var response = await HttpClient.PostAsJsonAsync(url, body, JsonOptions, cancellationToken);
        await EnsureSuccessOrThrowAsync(response, "POST odata/Cases/BookmarkedByCurrentState", cancellationToken);

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
