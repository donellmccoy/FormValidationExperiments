using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for persisting workflow state history snapshot entries.
/// Convention routing maps POST /odata/WorkflowStateHistory to this action.
/// Uses [FromBody] so ASP.NET's System.Text.Json formatter handles deserialization
/// (same pattern as CasesController.Post).
/// </summary>
[Authorize]
public class WorkflowStateHistoryController : ODataControllerBase
{
    public WorkflowStateHistoryController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>
    /// Returns an IQueryable of workflow state history entries for OData query composition.
    /// OData route: GET /odata/WorkflowStateHistory
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingWorkflowStateHistories();
        var context = await CreateContextAsync(ct);
        return Ok(context.WorkflowStateHistories.AsNoTracking());
    }

    /// <summary>
    /// Returns a single workflow state history entry by key.
    /// OData route: GET /odata/WorkflowStateHistory({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingWorkflowStateHistory(key);
        var context = await CreateContextAsync(ct);
        return Ok(SingleResult.Create(context.WorkflowStateHistories.AsNoTracking().Where(h => h.Id == key)));
    }

    /// <summary>
    /// Creates a single workflow state history entry.
    /// OData route: POST /odata/WorkflowStateHistory
    /// </summary>
    /// <param name="entry">The workflow state history to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        return Created(entry);
    }

    /// <summary>
    /// Updates a workflow state history entry (typically to set EndDate when completing a step).
    /// OData route: PATCH /odata/WorkflowStateHistory({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<WorkflowStateHistory> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return ValidationProblem(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.WorkflowStateHistories.FindAsync([key], ct);

        if (existing is null)
        {
            return Problem(title: "Not found", detail: $"No workflow state history entry exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ExitDate" };
        var changed = delta.GetChangedPropertyNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!changed.IsSubsetOf(allowed))
        {
            return Problem(
                title: "Invalid update",
                detail: "Only ExitDate can be updated on workflow state history entries.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var originalRowVersion = existing.RowVersion;
        delta.Patch(existing);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        return Updated(existing);
    }
}