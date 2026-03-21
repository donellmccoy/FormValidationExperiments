using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

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
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
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
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
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
    /// Creates a single workflow state history entry.
    /// OData route: POST /odata/WorkflowStateHistories
    /// </summary>
    /// <param name="dto">The workflow state history DTO to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] CreateWorkflowStateHistoryDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        var entry = WorkflowStateHistoryDtoMapper.ToEntity(dto);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.WorkflowStateHistories.Add(entry);
        await context.SaveChangesAsync(ct);

        return Created(entry);
    }

    /// <summary>
    /// Creates multiple workflow state history entries in a single batch.
    /// Custom route: POST /odata/WorkflowStateHistories/Batch
    /// </summary>
    /// <param name="dtos">The workflow state history DTOs to persist.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("odata/WorkflowStateHistories/Batch")]
    public async Task<IActionResult> PostBatch([FromBody] List<CreateWorkflowStateHistoryDto> dtos, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.WorkflowStateHistoryInvalidModelState();
            return BadRequest(ModelState);
        }

        if (dtos is not { Count: > 0 })
        {
            return BadRequest("At least one entry is required.");
        }

        var entries = dtos.Select(WorkflowStateHistoryDtoMapper.ToEntity).ToList();

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.WorkflowStateHistories.AddRange(entries);
        await context.SaveChangesAsync(ct);

        return Ok(entries);
    }
}