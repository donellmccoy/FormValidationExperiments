using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for persisting workflow state history snapshot entries.
/// Convention routing maps POST /odata/WorkflowStateHistories to this action.
/// Uses [FromBody] so ASP.NET's System.Text.Json formatter handles deserialization
/// (same pattern as CasesController.Post).
/// </summary>
[Authorize]
public class WorkflowStateHistoriesController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public WorkflowStateHistoriesController(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        entry.LineOfDutyCase = null; // Avoid re-inserting the parent entity
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        return Created(entry);
    }
}
