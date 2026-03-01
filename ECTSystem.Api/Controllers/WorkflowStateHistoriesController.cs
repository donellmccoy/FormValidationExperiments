using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
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
    private readonly IApiLogService _log;

    public WorkflowStateHistoriesController(IDbContextFactory<EctDbContext> contextFactory, IApiLogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _log.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (entry.LineOfDutyCaseId <= 0)
        {
            _log.WorkflowStateHistoryInvalidCaseId(entry.LineOfDutyCaseId);
            return BadRequest("LineOfDutyCaseId is required.");
        }

        _log.CreatingWorkflowStateHistory(entry.LineOfDutyCaseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        entry.LineOfDutyCase = null; // Avoid re-inserting the parent entity
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        _log.WorkflowStateHistoryCreated(entry.Id, entry.LineOfDutyCaseId);
        return Created(entry);
    }
}
