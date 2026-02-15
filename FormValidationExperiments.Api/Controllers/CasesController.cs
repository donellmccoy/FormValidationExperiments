using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using FormValidationExperiments.Api.Data;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.Api.Controllers;

/// <summary>
/// OData-enabled controller for LOD case CRUD operations.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
public class CasesController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public CasesController(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        // Create a long-lived context — OData needs the query to remain open
        // until the response is serialized. The context will be disposed by the DI scope.
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Cases.AsNoTracking());
    }

    /// <summary>
    /// Returns a single LOD case by key with all navigation properties.
    /// OData route: GET /odata/Cases({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromRoute] int key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await CaseWithIncludes(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == key);

        return lodCase is null ? NotFound() : Ok(lodCase);
    }

    /// <summary>
    /// Creates a new LOD case.
    /// OData route: POST /odata/Cases
    /// </summary>
    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync();

        return Created(lodCase);
    }

    /// <summary>
    /// Fully replaces an existing LOD case.
    /// OData route: PUT /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Put([FromRoute] int key, [FromBody] LineOfDutyCase update)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (existing is null)
            return NotFound();

        update.Id = key;
        context.Entry(existing).CurrentValues.SetValues(update);
        await context.SaveChangesAsync();

        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing LOD case.
    /// OData route: PATCH /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromRoute] int key, [FromBody] Delta<LineOfDutyCase> delta)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (existing is null)
            return NotFound();

        delta.Patch(existing);
        await context.SaveChangesAsync();

        return Updated(existing);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromRoute] int key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (lodCase is null)
            return NotFound();

        context.TimelineSteps.RemoveRange(lodCase.TimelineSteps);
        context.Authorities.RemoveRange(lodCase.Authorities);
        context.Documents.RemoveRange(lodCase.Documents);
        context.Appeals.RemoveRange(lodCase.Appeals);
        context.Cases.Remove(lodCase);
        await context.SaveChangesAsync();

        return NoContent();
    }

    private static IQueryable<LineOfDutyCase> CaseWithIncludes(EctDbContext context)
    {
        return context.Cases
            .AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps).ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP);
    }
}
