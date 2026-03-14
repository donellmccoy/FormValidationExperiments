using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
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
    /// Returns an IQueryable of workflow state history entries for OData query composition.
    /// OData route: GET /odata/WorkflowStateHistories
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var context = await CreateContextAsync(ct);
        return Ok(context.WorkflowStateHistories.AsNoTracking());
    }

    /// <summary>
    /// Returns a single workflow state history entry by key.
    /// OData route: GET /odata/WorkflowStateHistories({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
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
    [EnableQuery]
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
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        _loggingService.WorkflowStateHistoryCreated(entry.Id, entry.LineOfDutyCaseId);
        return Created(entry);
    }

    /// <summary>
    /// Creates multiple workflow state history entries atomically for the specified case.
    /// Route: POST /odata/WorkflowStateHistories/Batch
    /// All entries are persisted in a single SaveChangesAsync call for transactional consistency.
    /// </summary>
    /// <param name="entries">The list of workflow state history entries to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost]
    public async Task<IActionResult> Batch([FromBody] List<WorkflowStateHistory> entries, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (entries is not { Count: > 0 })
        {
            _loggingService.WorkflowStateHistoryBatchEmpty();
            return BadRequest("At least one entry is required.");
        }

        var caseId = entries[0].LineOfDutyCaseId;

        if (entries.Any(e => e.LineOfDutyCaseId <= 0))
        {
            _loggingService.WorkflowStateHistoryInvalidCaseId(caseId);
            return BadRequest("All entries must have a valid LineOfDutyCaseId.");
        }

        _loggingService.CreatingWorkflowStateHistoryBatch(entries.Count, caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        context.WorkflowStateHistories.AddRange(entries);
        await context.SaveChangesAsync(ct);

        _loggingService.WorkflowStateHistoryBatchCreated(entries.Count, caseId);
        return Ok(entries);
    }

    /// <summary>
    /// Creates a scoped <see cref="EctDbContext"/> and registers it for disposal at the end of the HTTP response.
    /// Use this helper when returning an <see cref="IQueryable"/> so the context remains alive during serialization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="EctDbContext"/> registered for response-lifetime disposal.</returns>
    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }
}
