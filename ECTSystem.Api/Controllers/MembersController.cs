using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for Member CRUD operations.
/// Named "MembersController" to match the OData entity set "Members" (convention routing).
/// </summary>
public class MembersController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public MembersController(IDbContextFactory<EctDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>
    /// Returns an IQueryable of Members for OData query composition.
    /// OData route: GET /odata/Members
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Members.AsNoTracking());
    }

    /// <summary>
    /// Returns a single Member by key with navigation properties.
    /// OData route: GET /odata/Members({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromRoute] int key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var member = await context.Members
            .Include(m => m.LineOfDutyCases)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == key);

        return member is null ? NotFound() : Ok(member);
    }

    /// <summary>
    /// Creates a new Member.
    /// OData route: POST /odata/Members
    /// </summary>
    public async Task<IActionResult> Post([FromBody] Member member)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Members.Add(member);
        await context.SaveChangesAsync();

        return Created(member);
    }

    /// <summary>
    /// Fully replaces an existing Member.
    /// OData route: PUT /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Put([FromRoute] int key, [FromBody] Member update)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Members.FindAsync(key);
        if (existing is null)
            return NotFound();

        update.Id = key;
        context.Entry(existing).CurrentValues.SetValues(update);
        await context.SaveChangesAsync();

        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing Member.
    /// OData route: PATCH /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromRoute] int key, [FromBody] Delta<Member> delta)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Members.FindAsync(key);
        if (existing is null)
            return NotFound();

        delta.Patch(existing);
        await context.SaveChangesAsync();

        return Updated(existing);
    }

    /// <summary>
    /// Deletes a Member.
    /// OData route: DELETE /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromRoute] int key)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var member = await context.Members.FindAsync(key);
        if (member is null)
            return NotFound();

        context.Members.Remove(member);
        await context.SaveChangesAsync();

        return NoContent();
    }
}
