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
public class CaseBookmarksController : ODataController
{
    private readonly ICaseBookmarkService _bookmarkService;

    // Hardcoded until authentication is implemented.
    private const string DefaultUserId = "System";

    public CaseBookmarksController(ICaseBookmarkService bookmarkService)
    {
        _bookmarkService = bookmarkService;
    }

    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        return Ok(_bookmarkService.GetBookmarksQueryable(DefaultUserId));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CaseBookmark bookmark)
    {
        var created = await _bookmarkService.AddBookmarkAsync(DefaultUserId, bookmark.LineOfDutyCaseId);
        return Created(created);
    }

    [HttpDelete("odata/CaseBookmarks/DeleteByCaseId")]
    public async Task<IActionResult> DeleteByCaseId([FromQuery] int caseId)
    {
        var removed = await _bookmarkService.RemoveBookmarkAsync(DefaultUserId, caseId);
        if (!removed)
        {
            return NotFound();
        }

        return NoContent();
    }

    [HttpGet("odata/CaseBookmarks/IsBookmarked(caseId={caseId})")]
    public async Task<IActionResult> IsBookmarked([FromRoute] int caseId)
    {
        var result = await _bookmarkService.IsBookmarkedAsync(DefaultUserId, caseId);
        return Ok(new { Value = result });
    }
}
