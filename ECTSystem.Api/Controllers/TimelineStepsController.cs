using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for timeline step operations.
/// Named "TimelineStepsController" to match the OData entity set "TimelineSteps" (convention routing).
/// </summary>
[Authorize]
public class TimelineStepsController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IApiLogService _log;

    public TimelineStepsController(IDbContextFactory<EctDbContext> contextFactory, IApiLogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Digitally signs a timeline step, recording the current user and UTC timestamp.
    /// OData action route: POST /odata/TimelineSteps({key})/Sign
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Sign([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.SigningTimelineStep(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var step = await context.TimelineSteps.FindAsync([key], ct);

        if (step is null)
        {
            _log.TimelineStepNotFound(key);
            return NotFound();
        }

        step.SignedDate = DateTime.UtcNow;
        step.SignedBy = UserId;
        await context.SaveChangesAsync(ct);

        _log.TimelineStepSigned(key);
        return Ok(step);
    }

    /// <summary>
    /// Sets the StartDate on a timeline step to UTC now.
    /// OData action route: POST /odata/TimelineSteps({key})/Start
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.StartingTimelineStep(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var step = await context.TimelineSteps.FindAsync([key], ct);

        if (step is null)
        {
            _log.TimelineStepNotFound(key);
            return NotFound();
        }

        step.StartDate = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        _log.TimelineStepStarted(key);
        return Ok(step);
    }
}
