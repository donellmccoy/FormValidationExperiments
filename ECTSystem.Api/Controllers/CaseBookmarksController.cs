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
    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>
    /// Initializes a new instance of <see cref="CaseBookmarksController"/>.
    /// </summary>
    /// <param name="loggingService">The structured logging service.</param>
    /// <param name="contextFactory">The EF Core context factory.</param>
    public CaseBookmarksController(ILoggingService loggingService, IDbContextFactory<EctDbContext> contextFactory)
    {
        _loggingService = loggingService;
        _contextFactory = contextFactory;
    }

    /// <summary>Gets the authenticated user's unique identifier from the JWT claims.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the user is not authenticated.</exception>
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Returns all bookmarks owned by the current user.
    /// OData route: GET /odata/CaseBookmarks
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _loggingService.QueryingBookmarks();
        var context = await CreateContextAsync(ct);
        return Ok(context.CaseBookmarks.AsNoTracking().Where(b => b.UserId == UserId));
    }

    /// <summary>
    /// Creates a new bookmark for the current user, or returns the existing one if already bookmarked.
    /// OData route: POST /odata/CaseBookmarks
    /// </summary>
    /// <param name="bookmark">The bookmark to create; only <c>LineOfDutyCaseId</c> is required.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == bookmark.LineOfDutyCaseId, ct);

        if (existing is not null)
        {
            _loggingService.BookmarkAlreadyExists(bookmark.LineOfDutyCaseId);
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
        _loggingService.BookmarkCreated(bookmark.LineOfDutyCaseId);
        return Created(newBookmark);
    }

    /// <summary>
    /// Removes the current user's bookmark for the specified case.
    /// OData action route: POST /odata/CaseBookmarks/DeleteByCaseId
    /// </summary>
    /// <param name="parameters">OData action parameters; must contain integer <c>caseId</c>.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> DeleteByCaseId(ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (!parameters.TryGetValue("caseId", out var caseIdObj) || caseIdObj is not int caseId)
        {
            return BadRequest("caseId is required.");
        }

        _loggingService.DeletingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var bookmark = await context.CaseBookmarks
            .FirstOrDefaultAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId, ct);

        if (bookmark is null)
        {
            _loggingService.BookmarkNotFound(caseId);
            return NotFound();
        }

        context.CaseBookmarks.Remove(bookmark);
        await context.SaveChangesAsync(ct);
        _loggingService.BookmarkDeleted(caseId);
        return NoContent();
    }

    /// <summary>
    /// Returns whether the current user has bookmarked the specified case.
    /// OData function route: GET /odata/CaseBookmarks/IsBookmarked(caseId={caseId})
    /// </summary>
    /// <param name="caseId">The LOD case identifier to check.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet]
    public async Task<IActionResult> IsBookmarked([FromODataUri] int caseId, CancellationToken ct = default)
    {
        _loggingService.CheckingBookmark(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var isBookmarked = await context.CaseBookmarks
            .AnyAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == caseId, ct);
        return Ok(new { Value = isBookmarked });
    }

    /// <summary>
    /// Creates a scoped <see cref="EctDbContext"/> and registers it for disposal at the end of the HTTP response.
    /// Use this helper when returning an <see cref="IQueryable"/> so the context remains alive during serialization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="EctDbContext"/> registered for response-lifetime disposal.</returns>
    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }
}
