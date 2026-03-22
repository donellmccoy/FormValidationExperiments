using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for case bookmark operations.
/// Named "CaseBookmarksController" to match the OData entity set "CaseBookmarks" (convention routing).
/// </summary>
[Authorize]
public class CaseBookmarksController : ODataControllerBase
{
    public CaseBookmarksController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>Gets the authenticated user's unique identifier from the JWT claims.</summary>
    private string GetUserId() => User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "test-user-id";

    /// <summary>
    /// Returns all bookmarks owned by the current user.
    /// OData route: GET /odata/CaseBookmarks
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingBookmarks();
        var context = await CreateContextAsync(ct);
        return Ok(context.CaseBookmarks.AsNoTracking().Where(b => b.UserId == GetUserId()));
    }

    /// <summary>
    /// Creates a new bookmark for the current user, or returns the existing one if already bookmarked.
    /// OData route: POST /odata/CaseBookmarks
    /// </summary>
    /// <param name="bookmark">The bookmark entity; only <c>LineOfDutyCaseId</c> is required.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == GetUserId() && b.LineOfDutyCaseId == bookmark.LineOfDutyCaseId, ct);

        if (existing is not null)
        {
            LoggingService.BookmarkAlreadyExists(bookmark.LineOfDutyCaseId);
            return Ok(existing);
        }

        bookmark.UserId = GetUserId();
        bookmark.BookmarkedDate = DateTime.UtcNow;

        context.CaseBookmarks.Add(bookmark);
        await context.SaveChangesAsync(ct);
        LoggingService.BookmarkCreated(bookmark.LineOfDutyCaseId);
        return Created(bookmark);
    }

    /// <summary>
    /// Deletes a bookmark by its primary key.
    /// OData route: DELETE /odata/CaseBookmarks({key})
    /// </summary>
    /// <param name="key">The bookmark identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.CaseBookmarks.FirstOrDefaultAsync(b => b.Id == key && b.UserId == GetUserId(), ct);

        if (bookmark is null)
        {
            return NotFound();
        }

        LoggingService.DeletingBookmark(bookmark.LineOfDutyCaseId);
        context.CaseBookmarks.Remove(bookmark);
        await context.SaveChangesAsync(ct);
        LoggingService.BookmarkDeleted(bookmark.LineOfDutyCaseId);
        return NoContent();
    }
}
