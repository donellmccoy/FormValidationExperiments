using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for LOD case CRUD operations.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
public class CasesController : ODataController
{
    private readonly IDataService _dataService;
    private readonly IApiLogService _log;

    public CasesController(IDataService dataService, IApiLogService log)
    {
        _dataService = dataService;
        _log = log;
    }

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        _log.QueryingCases();
        return Ok(_dataService.GetCasesQueryable());
    }

    /// <summary>
    /// Returns a single LOD case by key with all navigation properties.
    /// OData route: GET /odata/Cases({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromRoute] int key)
    {
        _log.RetrievingCase(key);
        var lodCase = await _dataService.GetCaseByKeyAsync(key);

        if (lodCase is null)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        return Ok(lodCase);
    }

    /// <summary>
    /// Creates a new LOD case.
    /// OData route: POST /odata/Cases
    /// </summary>
    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase)
    {
        if (!ModelState.IsValid)
        {
            _log.InvalidModelState("Post");
            return BadRequest(ModelState);
        }

        var created = await _dataService.CreateCaseAsync(lodCase);

        _log.CaseCreated(created.Id);
        return Created(created);
    }

    /// <summary>
    /// Partially updates an existing LOD case using OData Delta semantics.
    /// Only the properties present in the request body are applied to the entity.
    /// OData route: PATCH /odata/Cases({key})
    /// </summary>
    /// <remarks>
    /// Do NOT add [FromBody] — OData's own input formatter must handle Delta&lt;T&gt;
    /// deserialization. [FromBody] would route through SystemTextJsonInputFormatter
    /// which cannot construct a Delta instance.
    /// </remarks>
    public async Task<IActionResult> Patch([FromRoute] int key, Delta<LineOfDutyCase> delta)
    {
        if (delta is null || !ModelState.IsValid)
        {
            _log.InvalidModelState("Patch");
            return BadRequest(ModelState);
        }

        _log.PatchingCase(key);
        var updated = await _dataService.PatchCaseAsync(key, delta);
        if (updated is null)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        _log.CasePatched(key);
        return Updated(updated);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromRoute] int key)
    {
        _log.DeletingCase(key);
        var deleted = await _dataService.DeleteCaseAsync(key);
        if (!deleted)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        _log.CaseDeleted(key);
        return NoContent();
    }
}
