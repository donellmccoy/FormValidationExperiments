using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
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
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _log.QueryingBookmarks();
        var context = await CreateContextAsync(ct);
        return Ok(context.CaseBookmarks.AsNoTracking().Where(b => b.UserId == UserId));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == bookmark.LineOfDutyCaseId, ct);

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
        await context.SaveChangesAsync(ct);
        _log.BookmarkCreated(bookmark.LineOfDutyCaseId);
        return Created(newBookmark);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteByCaseId(ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetValue("caseId", out var caseIdObj) || caseIdObj is not int caseId)
        {
            return BadRequest("caseId is required.");
        }

        _log.DeletingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId, ct);

        if (bookmark is null)
        {
            _log.BookmarkNotFound(caseId);
            return NotFound();
        }

        context.CaseBookmarks.Remove(bookmark);
        await context.SaveChangesAsync(ct);
        _log.BookmarkDeleted(caseId);
        return NoContent();
    }

    [HttpGet]
    public async Task<IActionResult> IsBookmarked([FromODataUri] int caseId, CancellationToken ct = default)
    {
        _log.CheckingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var isBookmarked = await context.CaseBookmarks
            .AnyAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId, ct);
        return Ok(new { Value = isBookmarked });
    }

    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }
}
