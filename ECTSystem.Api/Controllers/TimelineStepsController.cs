using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ECTSystem.Api.Services;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for timeline step operations.
/// Named "TimelineStepsController" to match the OData entity set "TimelineSteps" (convention routing).
/// </summary>
[Authorize]
public class TimelineStepsController : ODataController
{
    private readonly ILineOfDutyTimelineService _timelineService;

    public TimelineStepsController(ILineOfDutyTimelineService timelineService)
    {
        _timelineService = timelineService;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Digitally signs a timeline step, recording the current user and UTC timestamp.
    /// OData action route: POST /odata/TimelineSteps({key})/Sign
    /// </summary>
    [HttpPost("odata/TimelineSteps({key})/Sign")]
    public async Task<IActionResult> Sign([FromRoute] int key, CancellationToken ct)
    {
        try
        {
            var step = await _timelineService.SignTimelineStepAsync(key, UserId, ct);
            return Ok(step);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }

    /// <summary>
    /// Sets the StartDate on a timeline step to UTC now.
    /// OData action route: POST /odata/TimelineSteps({key})/Start
    /// </summary>
    [HttpPost("odata/TimelineSteps({key})/Start")]
    public async Task<IActionResult> Start([FromRoute] int key, CancellationToken ct)
    {
        try
        {
            var step = await _timelineService.StartTimelineStepAsync(key, ct);
            return Ok(step);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }
    }
}
