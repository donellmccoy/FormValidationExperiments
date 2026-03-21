using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class BookmarkHttpService : ODataServiceBase, IBookmarkService
{
    public BookmarkHttpService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get all bookmarked case IDs for the current user.
        var bookmarkQuery = Context.CaseBookmarks
            .AddQueryOption("$select", "LineOfDutyCaseId");

        var bookmarks = await ExecuteQueryAsync(bookmarkQuery, cancellationToken);
        var bookmarkedIds = bookmarks.Select(b => b.LineOfDutyCaseId).ToList();

        if (bookmarkedIds.Count == 0)
        {
            return new ODataServiceResult<LineOfDutyCase> { Value = [], Count = 0 };
        }

        // Step 2: Query Cases filtered to the bookmarked IDs.
        var idFilter = $"Id in ({string.Join(",", bookmarkedIds)})";
        var combinedFilter = string.IsNullOrEmpty(filter) ? idFilter : $"({idFilter}) and ({filter})";

        var caseQuery = Context.Cases
            .AddQueryOption("$filter", combinedFilter);

        if (top.HasValue)
            caseQuery = caseQuery.AddQueryOption("$top", top.Value);

        if (skip.HasValue)
            caseQuery = caseQuery.AddQueryOption("$skip", skip.Value);

        if (!string.IsNullOrEmpty(orderby))
            caseQuery = caseQuery.AddQueryOption("$orderby", orderby);

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(caseQuery, cancellationToken);

            return new ODataServiceResult<LineOfDutyCase>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(caseQuery, cancellationToken);

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
        var response = await HttpClient.PostAsJsonAsync("odata/CaseBookmarks", dto, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Context.CaseBookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
            .AddQueryOption("$top", 1)
            .AddQueryOption("$select", "Id");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);
        var bookmark = bookmarks.FirstOrDefault();

        if (bookmark is null || bookmark.Id == 0)
        {
            return;
        }

        Context.AttachTo("CaseBookmarks", bookmark);
        Context.DeleteObject(bookmark);
        await Context.SaveChangesAsync(cancellationToken);
        Context.Detach(bookmark);
    }

    public async Task<bool> IsBookmarkedAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Context.CaseBookmarks
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
        var query = Context.CaseBookmarks
            .AddQueryOption("$filter", $"LineOfDutyCaseId in ({ids})")
            .AddQueryOption("$select", "LineOfDutyCaseId");

        var bookmarks = await ExecuteQueryAsync(query, cancellationToken);

        return bookmarks.Select(b => b.LineOfDutyCaseId).ToHashSet();
    }
}
