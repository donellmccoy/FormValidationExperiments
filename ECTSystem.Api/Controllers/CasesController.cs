using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for LOD case CRUD operations.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
public partial class CasesController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly ILogger<CasesController> _logger;

    public CasesController(IDbContextFactory<EctDbContext> contextFactory, ILogger<CasesController> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        Log.QueryingCases(_logger);
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
        Log.RetrievingCase(_logger, key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await CaseWithIncludes(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == key);

        if (lodCase is null)
        {
            Log.CaseNotFound(_logger, key);
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
            Log.InvalidModelState(_logger, "Post");
            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync();

        Log.CaseCreated(_logger, lodCase.Id);
        return Created(lodCase);
    }

    /// <summary>
    /// Fully replaces an existing LOD case.
    /// OData route: PUT /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Put([FromRoute] int key, [FromBody] LineOfDutyCase update)
    {
        if (!ModelState.IsValid)
        {
            Log.InvalidModelState(_logger, "Put");
            return BadRequest(ModelState);
        }

        Log.UpdatingCase(_logger, key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (existing is null)
        {
            Log.CaseNotFound(_logger, key);
            return NotFound();
        }

        // 1. Update scalar properties on the root entity
        update.Id = key;
        update.MemberId = existing.MemberId;   // preserve FK — not editable via form
        update.MEDCONId = existing.MEDCONId;
        update.INCAPId = existing.INCAPId;
        context.Entry(existing).CurrentValues.SetValues(update);

        // 2. Synchronize the Authorities collection (ApplyAll modifies authorities)
        SyncAuthorities(context, existing, update.Authorities);

        // 3. Update MEDCON / INCAP scalar properties
        if (existing.MEDCON is not null && update.MEDCON is not null)
            context.Entry(existing.MEDCON).CurrentValues.SetValues(update.MEDCON);

        if (existing.INCAP is not null && update.INCAP is not null)
            context.Entry(existing.INCAP).CurrentValues.SetValues(update.INCAP);

        await context.SaveChangesAsync();

        Log.CaseUpdated(_logger, key);
        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing LOD case.
    /// OData route: PATCH /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromRoute] int key, [FromBody] Delta<LineOfDutyCase> delta)
    {
        if (!ModelState.IsValid)
        {
            Log.InvalidModelState(_logger, "Patch");
            return BadRequest(ModelState);
        }

        Log.PatchingCase(_logger, key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (existing is null)
        {
            Log.CaseNotFound(_logger, key);
            return NotFound();
        }

        delta.Patch(existing);
        await context.SaveChangesAsync();

        Log.CasePatched(_logger, key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromRoute] int key)
    {
        Log.DeletingCase(_logger, key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var lodCase = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key);
        if (lodCase is null)
        {
            Log.CaseNotFound(_logger, key);
            return NotFound();
        }

        context.TimelineSteps.RemoveRange(lodCase.TimelineSteps);
        context.Authorities.RemoveRange(lodCase.Authorities);
        context.Documents.RemoveRange(lodCase.Documents);
        context.Appeals.RemoveRange(lodCase.Appeals);
        context.Notifications.RemoveRange(lodCase.Notifications);
        context.Cases.Remove(lodCase);
        await context.SaveChangesAsync();

        Log.CaseDeleted(_logger, key);
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
            .Include(c => c.Member)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .Include(c => c.Notifications);
    }

    /// <summary>
    /// Synchronizes the Authorities navigation collection: updates existing,
    /// adds new, and removes deleted items.
    /// </summary>
    private static void SyncAuthorities(
        EctDbContext context,
        LineOfDutyCase existing,
        List<LineOfDutyAuthority> incoming)
    {
        incoming ??= [];

        // Remove authorities no longer present
        var incomingIds = incoming.Where(a => a.Id != 0).Select(a => a.Id).ToHashSet();
        var toRemove = existing.Authorities.Where(a => !incomingIds.Contains(a.Id)).ToList();
        foreach (var auth in toRemove)
        {
            existing.Authorities.Remove(auth);
            context.Authorities.Remove(auth);
        }

        foreach (var updatedAuth in incoming)
        {
            var existingAuth = updatedAuth.Id != 0
                ? existing.Authorities.FirstOrDefault(a => a.Id == updatedAuth.Id)
                : null;

            if (existingAuth is not null)
            {
                // Update scalar properties on the tracked authority
                context.Entry(existingAuth).CurrentValues.SetValues(updatedAuth);
            }
            else
            {
                // New authority — ensure clean state and add
                updatedAuth.Id = 0;
                updatedAuth.LineOfDutyCaseId = existing.Id;
                existing.Authorities.Add(updatedAuth);
            }
        }
    }
}
