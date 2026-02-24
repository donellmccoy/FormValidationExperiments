using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for persisting workflow step history snapshot entries.
/// Convention routing maps POST /odata/WorkflowStepHistories to this action.
/// Uses [FromBody] so ASP.NET's System.Text.Json formatter handles deserialization
/// (same pattern as CasesController.Post).
/// </summary>
[Authorize]
public class WorkflowStepHistoriesController : ODataController
{
    private readonly IWorkflowStepHistoryService _historyService;

    public WorkflowStepHistoriesController(IWorkflowStepHistoryService historyService)
    {
        _historyService = historyService;
    }

    public async Task<IActionResult> Post([FromBody] WorkflowStepHistory entry, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var saved = await _historyService.AddHistoryEntryAsync(entry, ct);
        return Created(saved);
    }
}
