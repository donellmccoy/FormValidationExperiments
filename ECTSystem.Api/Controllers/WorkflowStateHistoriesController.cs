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
    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of <see cref="WorkflowStateHistoriesController"/>.
    /// </summary>
    /// <param name="contextFactory">The EF Core context factory.</param>
    /// <param name="loggingService">The structured logging service.</param>
    public WorkflowStateHistoriesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
    {
        _contextFactory = contextFactory;
        _loggingService = loggingService;
    }

    /// <summary>
    /// Creates a new workflow state history entry for the specified case.
    /// OData route: POST /odata/WorkflowStateHistories
    /// </summary>
    /// <param name="entry">The workflow state history entry to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (entry.LineOfDutyCaseId <= 0)
        {
            _loggingService.WorkflowStateHistoryInvalidCaseId(entry.LineOfDutyCaseId);
            return BadRequest("LineOfDutyCaseId is required.");
        }

        _loggingService.CreatingWorkflowStateHistory(entry.LineOfDutyCaseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        entry.LineOfDutyCase = null; // Avoid re-inserting the parent entity
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        _loggingService.WorkflowStateHistoryCreated(entry.Id, entry.LineOfDutyCaseId);
        return Created(entry);
    }
}
