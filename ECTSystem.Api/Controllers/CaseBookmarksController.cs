using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
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
public class CaseBookmarksController : ODataController
{
    private readonly IApiLogService _log;
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public CaseBookmarksController(IApiLogService log, IDbContextFactory<EctDbContext> contextFactory)
    {
        _log = log;
        _contextFactory = contextFactory;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        _log.QueryingBookmarks();
        var context = _contextFactory.CreateDbContext();
        return Ok(context.CaseBookmarks.AsNoTracking().Where(b => b.UserId == UserId));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        var existing = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == bookmark.LineOfDutyCaseId);

        if (existing is not null)
        {
            _log.BookmarkAlreadyExists(bookmark.LineOfDutyCaseId);
            return Created(existing);
        }

        var newBookmark = new CaseBookmark
        {
            UserId = UserId,
            LineOfDutyCaseId = bookmark.LineOfDutyCaseId,
            BookmarkedDate = DateTime.UtcNow
        };

        context.CaseBookmarks.Add(newBookmark);
        await context.SaveChangesAsync();
        _log.BookmarkCreated(bookmark.LineOfDutyCaseId);
        return Created(newBookmark);
    }

    [HttpDelete("odata/CaseBookmarks/DeleteByCaseId")]
    public async Task<IActionResult> DeleteByCaseId([FromQuery] int caseId)
    {
        _log.DeletingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var bookmark = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId);

        if (bookmark is null)
        {
            _log.BookmarkNotFound(caseId);
            return NotFound();
        }

        context.CaseBookmarks.Remove(bookmark);
        await context.SaveChangesAsync();
        _log.BookmarkDeleted(caseId);
        return NoContent();
    }

    [HttpGet("odata/CaseBookmarks/IsBookmarked(caseId={caseId})")]
    public async Task<IActionResult> IsBookmarked([FromRoute] int caseId)
    {
        _log.CheckingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var isBookmarked = await context.CaseBookmarks
            .AnyAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId);
        return Ok(new { Value = isBookmarked });
    }
}
