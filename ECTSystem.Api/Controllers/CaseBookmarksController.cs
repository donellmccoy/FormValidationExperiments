using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for case bookmark operations.
/// Named "CaseBookmarksController" to match the OData entity set "CaseBookmarks" (convention routing).
/// </summary>
[Authorize]
public class CaseBookmarksController : ODataController
{
    private readonly ICaseBookmarkService _bookmarkService;

    public CaseBookmarksController(ICaseBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        return Ok(_bookmarkService.GetBookmarksQueryable(UserId));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark)
    {
        return Created(await _bookmarkService.AddBookmarkAsync(UserId, bookmark.LineOfDutyCaseId));
    }

    [HttpDelete("odata/CaseBookmarks/DeleteByCaseId")]
    public async Task<IActionResult> DeleteByCaseId([FromQuery] int caseId)
    {
        var removed = await _bookmarkService.RemoveBookmarkAsync(UserId, caseId);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("odata/CaseBookmarks/IsBookmarked(caseId={caseId})")]
    public async Task<IActionResult> IsBookmarked([FromRoute] int caseId)
    {
        return Ok(new { Value = await _bookmarkService.IsBookmarkedAsync(UserId, caseId) });
    }
}
