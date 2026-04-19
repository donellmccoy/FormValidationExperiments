using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for case bookmark operations.
/// Named "BookmarksController" to match the OData entity set "Bookmarks" (convention routing).
/// </summary>
[Authorize]
public class BookmarksController : ODataControllerBase
{
    public BookmarksController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService, TimeProvider timeProvider)
        : base(contextFactory, loggingService, timeProvider)
    {
    }

    /// <summary>
    /// Returns all bookmarks owned by the current user.
    /// OData route: GET /odata/Bookmarks
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingBookmarks();

        var context = await CreateContextAsync(ct);

        return Ok(context.Bookmarks.AsNoTracking().Where(b => b.UserId == GetAuthenticatedUserId()));
    }

    /// <summary>
    /// Creates a new bookmark for the current user, or returns the existing one if already bookmarked.
    /// OData route: POST /odata/Bookmarks
    /// </summary>
    /// <param name="dto">The bookmark data; only <c>LineOfDutyCaseId</c> is required.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Post([FromBody] CreateBookmarkDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var userId = GetAuthenticatedUserId();

        var bookmark = BookmarkDtoMapper.ToEntity(dto);

        bookmark.UserId = userId;

        context.Bookmarks.Add(bookmark);

        await context.SaveChangesAsync(ct);

        LoggingService.BookmarkCreated(dto.LineOfDutyCaseId);

        return Created(bookmark);
    }

    /// <summary>
    /// Deletes a bookmark by its primary key.
    /// OData route: DELETE /odata/Bookmarks({key})
    /// </summary>
    /// <param name="key">The bookmark identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        
        var bookmark = await context.Bookmarks.FirstOrDefaultAsync(b => b.Id == key && b.UserId == GetAuthenticatedUserId(), ct);

        if (bookmark is null)
        {
            return Problem(title: "Not found", detail: $"No bookmark exists with ID {key} for the current user.", statusCode: StatusCodes.Status404NotFound);
        }

        LoggingService.DeletingBookmark(bookmark.LineOfDutyCaseId);

        context.Bookmarks.Remove(bookmark);

        await context.SaveChangesAsync(ct);

        LoggingService.BookmarkDeleted(bookmark.LineOfDutyCaseId);

        return NoContent();
    }

    /// <summary>
    /// Creates a new bookmark for the current user and returns its ID.
    /// OData route: POST /odata/Bookmarks/AddBookmark
    /// </summary>
    /// <param name="parameters">OData action parameters: <c>caseId</c> (int).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> AddBookmark(ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (parameters is null || !parameters.TryGetValue("caseId", out var caseIdObj))
        {
            return BadRequest("The 'caseId' parameter is required.");
        }

        var caseId = (int)caseIdObj;

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var userId = GetAuthenticatedUserId();

        var existing = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.LineOfDutyCaseId == caseId && b.UserId == userId, ct);

        if (existing is not null)
        {
            return Ok(new { Id = existing.Id });
        }

        var bookmark = new Bookmark
        {
            LineOfDutyCaseId = caseId,
            UserId = userId
        };

        context.Bookmarks.Add(bookmark);

        await context.SaveChangesAsync(ct);

        LoggingService.BookmarkCreated(caseId);

        return Ok(new { bookmark.Id });
    }

    /// <summary>
    /// Deletes a bookmark by case ID and bookmark ID for the current user.
    /// OData route: POST /odata/Bookmarks/DeleteBookmark
    /// </summary>
    /// <param name="parameters">OData action parameters: <c>caseId</c> (int) and <c>bookmarkId</c> (int).</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> DeleteBookmark(ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (parameters is null
            || !parameters.TryGetValue("caseId", out var caseIdObj)
            || !parameters.TryGetValue("bookmarkId", out var bookmarkIdObj))
        {
            return BadRequest("Both 'caseId' and 'bookmarkId' parameters are required.");
        }

        var caseId = (int)caseIdObj;
        var bookmarkId = (int)bookmarkIdObj;

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var userId = GetAuthenticatedUserId();

        var bookmark = await context.Bookmarks
            .FirstOrDefaultAsync(b => b.Id == bookmarkId && b.LineOfDutyCaseId == caseId && b.UserId == userId, ct);

        if (bookmark is null)
        {
            return Problem(
                title: "Not found",
                detail: $"No bookmark exists with ID {bookmarkId} for case {caseId} for the current user.",
                statusCode: StatusCodes.Status404NotFound);
        }

        LoggingService.DeletingBookmark(bookmark.LineOfDutyCaseId);

        context.Bookmarks.Remove(bookmark);

        await context.SaveChangesAsync(ct);

        LoggingService.BookmarkDeleted(bookmark.LineOfDutyCaseId);

        return NoContent();
    }
}
