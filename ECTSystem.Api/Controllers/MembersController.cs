using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Api.Logging;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for Member CRUD operations.
/// Named "MembersController" to match the OData entity set "Members" (convention routing).
/// </summary>
[Authorize]
public class MembersController : ODataController
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IApiLogService _log;

    public MembersController(IDbContextFactory<EctDbContext> contextFactory, IApiLogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    /// <summary>
    /// Returns an IQueryable of Members for OData query composition.
    /// OData route: GET /odata/Members
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        _log.QueryingMembers();
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Members.AsNoTracking());
    }

    /// <summary>
    /// Returns a single Member by key with navigation properties.
    /// OData route: GET /odata/Members({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromODataUri] int key)
    {
        _log.RetrievingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var member = await context.Members
            //.Include(m => m.LineOfDutyCases)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == key);

        if (member is null)
        {
            _log.MemberNotFound(key);
            return NotFound();
        }

        return Ok(member);
    }

    /// <summary>
    /// Creates a new Member.
    /// OData route: POST /odata/Members
    /// </summary>
    public async Task<IActionResult> Post([FromBody] Member member)
    {
        if (!ModelState.IsValid)
        {
            _log.MemberInvalidModelState("Post");
            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync();
        context.Members.Add(member);
        await context.SaveChangesAsync();

        _log.MemberCreated(member.Id);
        return Created(member);
    }

    /// <summary>
    /// Fully replaces an existing Member.
    /// OData route: PUT /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] Member update)
    {
        if (!ModelState.IsValid)
        {
            _log.MemberInvalidModelState("Put");
            return BadRequest(ModelState);
        }

        _log.UpdatingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Members.FindAsync(key);
        if (existing is null)
        {
            _log.MemberNotFound(key);
            return NotFound();
        }

        update.Id = key;
        context.Entry(existing).CurrentValues.SetValues(update);
        await context.SaveChangesAsync();

        _log.MemberUpdated(key);
        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing Member.
    /// OData route: PATCH /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromRoute] int key, Delta<Member> delta)
    {
        if (!ModelState.IsValid)
        {
            _log.MemberInvalidModelState("Patch");
            return BadRequest(ModelState);
        }

        _log.PatchingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var existing = await context.Members.FindAsync(key);
        if (existing is null)
        {
            _log.MemberNotFound(key);
            return NotFound();
        }

        delta.Patch(existing);
        await context.SaveChangesAsync();

        _log.MemberPatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes a Member.
    /// OData route: DELETE /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromRoute] int key)
    {
        _log.DeletingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync();
        var member = await context.Members.FindAsync(key);
        if (member is null)
        {
            _log.MemberNotFound(key);
            return NotFound();
        }

        context.Members.Remove(member);
        await context.SaveChangesAsync();

        _log.MemberDeleted(key);
        return NoContent();
    }

    // ── Collection navigation properties ────────────────────────────────

    /// <summary>
    /// Returns LOD cases associated with a specific member.
    /// OData route: GET /odata/Members({key})/LineOfDutyCases
    /// </summary>
    [EnableQuery]
    public IActionResult GetLineOfDutyCases([FromRoute] int key)
    {
        _log.QueryingMemberNavigation(key, nameof(Member.LineOfDutyCases));
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Cases.AsNoTracking().Where(c => c.MemberId == key));
    }
}
