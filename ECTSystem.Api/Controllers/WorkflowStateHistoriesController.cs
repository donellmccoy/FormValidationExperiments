using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
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
public class WorkflowStateHistoriesController : ODataControllerBase
{
    public WorkflowStateHistoriesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>
    /// Returns an IQueryable of workflow state history entries for OData query composition.
    /// OData route: GET /odata/WorkflowStateHistories
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var context = await CreateContextAsync(ct);
        return Ok(context.WorkflowStateHistories.AsNoTracking());
    }

    /// <summary>
    /// Returns a single workflow state history entry by key.
    /// OData route: GET /odata/WorkflowStateHistories({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var entry = await context.WorkflowStateHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == key, ct);

        if (entry is null)
            return NotFound();

        return Ok(entry);
    }

    /// <summary>
    /// Creates a new workflow state history entry for the specified case.
    /// OData route: POST /odata/WorkflowStateHistories
    /// </summary>
    /// <param name="entry">The workflow state history entry to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (entry.LineOfDutyCaseId <= 0)
        {
            LoggingService.WorkflowStateHistoryInvalidCaseId(entry.LineOfDutyCaseId);
            return BadRequest("LineOfDutyCaseId is required.");
        }

        // Over-posting guard: reset server-managed fields
        entry.Id = 0;
        entry.CreatedBy = string.Empty;
        entry.CreatedDate = default;
        entry.ModifiedBy = string.Empty;
        entry.ModifiedDate = default;

        LoggingService.CreatingWorkflowStateHistory(entry.LineOfDutyCaseId);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        LoggingService.WorkflowStateHistoryCreated(entry.Id, entry.LineOfDutyCaseId);
        return Created(entry);
    }

    /// <summary>
    /// Creates multiple workflow state history entries in a single request.
    /// Custom route: POST /odata/WorkflowStateHistories/Batch
    /// </summary>
    /// <param name="entries">The workflow state history entries to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("odata/WorkflowStateHistories/Batch")]
    public async Task<IActionResult> PostBatch([FromBody] List<WorkflowStateHistory> entries, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (entries is not { Count: > 0 })
        {
            return BadRequest("At least one entry is required.");
        }

        foreach (var entry in entries)
        {
            if (entry.LineOfDutyCaseId <= 0)
            {
                LoggingService.WorkflowStateHistoryInvalidCaseId(entry.LineOfDutyCaseId);
                return BadRequest($"LineOfDutyCaseId is required for all entries.");
            }

            // Over-posting guard: reset server-managed fields
            entry.Id = 0;
            entry.CreatedBy = string.Empty;
            entry.CreatedDate = default;
            entry.ModifiedBy = string.Empty;
            entry.ModifiedDate = default;
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.WorkflowStateHistories.AddRange(entries);
        await context.SaveChangesAsync(ct);

        return Ok(entries);
    }
}
