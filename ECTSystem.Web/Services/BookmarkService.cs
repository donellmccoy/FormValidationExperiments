using ECTSystem.Shared.Models;
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
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Get all bookmarked case IDs for the current user.
        var bookmarkQuery = Context.Bookmarks
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

        var bookmark = new Bookmark { LineOfDutyCaseId = caseId };

        try
        {
            Context.AddObject("Bookmarks", bookmark);
            await Context.SaveChangesAsync(SaveChangesOptions.None, cancellationToken);
        }
        finally
        {
            Context.Detach(bookmark);
        }
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

        try
        {
            if (Context.GetEntityDescriptor(bookmark) == null)
            {
                Context.AttachTo("Bookmarks", bookmark);
            }
            Context.DeleteObject(bookmark);
            await Context.SaveChangesAsync(cancellationToken);
        }
        catch (DataServiceRequestException ex) when (ex.InnerException is DataServiceClientException { StatusCode: 404 })
        {
            // Already deleted on the server, safely ignore
        }
        finally
        {
            Context.Detach(bookmark);
        }
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
}
