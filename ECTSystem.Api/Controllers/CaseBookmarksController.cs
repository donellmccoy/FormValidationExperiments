using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

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
    /// <exception cref="InvalidOperationException">Thrown when the user is not authenticated.</exception>
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

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
        return Ok(context.CaseBookmarks.AsNoTracking().Where(b => b.UserId == UserId));
    }

    /// <summary>
    /// Creates a new bookmark for the current user, or returns the existing one if already bookmarked.
    /// OData route: POST /odata/CaseBookmarks
    /// </summary>
    /// <param name="dto">The bookmark DTO; only <c>LineOfDutyCaseId</c> is required.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateBookmarkDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == dto.LineOfDutyCaseId, ct);

        if (existing is not null)
        {
            LoggingService.BookmarkAlreadyExists(dto.LineOfDutyCaseId);
            return Ok(existing);
        }

        var newBookmark = new CaseBookmark
        {
            UserId = UserId,
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
            BookmarkedDate = DateTime.UtcNow
        };

        context.CaseBookmarks.Add(newBookmark);
        await context.SaveChangesAsync(ct);
        LoggingService.BookmarkCreated(dto.LineOfDutyCaseId);
        return Created(newBookmark);
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
        var bookmark = await context.CaseBookmarks.FirstOrDefaultAsync(b => b.Id == key && b.UserId == UserId, ct);

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