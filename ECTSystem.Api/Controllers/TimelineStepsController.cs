using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
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

    public TimelineStepsController(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Digitally signs a timeline step, recording the current user and UTC timestamp.
    /// OData action route: POST /odata/TimelineSteps({key})/Sign
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Sign([FromODataUri] int key, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var step = await context.TimelineSteps.FindAsync(new object[] { key }, ct);

        if (step is null)
        {
            return NotFound();
        }

        step.SignedDate = DateTime.UtcNow;
        step.SignedBy = UserId;
        await context.SaveChangesAsync(ct);

        return Ok(step);
    }

    /// <summary>
    /// Sets the StartDate on a timeline step to UTC now.
    /// OData action route: POST /odata/TimelineSteps({key})/Start
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromODataUri] int key, CancellationToken ct)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var step = await context.TimelineSteps.FindAsync(new object[] { key }, ct);

        if (step is null)
        {
            return NotFound();
        }

        step.StartDate = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);

        return Ok(step);
    }
}
