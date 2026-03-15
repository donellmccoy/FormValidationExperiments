using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
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
public class MembersController : ODataControllerBase
{
    public MembersController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>
    /// Returns an IQueryable of Members for OData query composition.
    /// OData route: GET /odata/Members
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingMembers();
        var context = await CreateContextAsync(ct);
        return Ok(context.Members.AsNoTracking());
    }

    /// <summary>
    /// Returns a single Member by key with navigation properties.
    /// OData route: GET /odata/Members({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var member = await context.Members
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == key, ct);

        if (member is null)
        {
            LoggingService.MemberNotFound(key);
            return NotFound();
        }

        return Ok(member);
    }

    /// <summary>
    /// Creates a new Member.
    /// OData route: POST /odata/Members
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] Member member, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.MemberInvalidModelState("Post");
            return BadRequest(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        // Over-posting guard: reset server-managed fields
        member.Id = 0;
        member.CreatedBy = string.Empty;
        member.CreatedDate = default;
        member.ModifiedBy = string.Empty;
        member.ModifiedDate = default;

        context.Members.Add(member);
        await context.SaveChangesAsync(ct);

        LoggingService.MemberCreated(member.Id);
        return Created(member);
    }

    /// <summary>
    /// Fully replaces an existing Member.
    /// OData route: PUT /odata/Members({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] Member update, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.MemberInvalidModelState("Put");
            return BadRequest(ModelState);
        }

        LoggingService.UpdatingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Members.FindAsync([key], ct);
        if (existing is null)
        {
            LoggingService.MemberNotFound(key);
            return NotFound();
        }

        update.Id = key;
        context.Entry(existing).CurrentValues.SetValues(update);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = update.RowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        LoggingService.MemberUpdated(key);
        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing Member.
    /// OData route: PATCH /odata/Members({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<Member> delta, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.MemberInvalidModelState("Patch");
            return BadRequest(ModelState);
        }

        LoggingService.PatchingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Members.FindAsync([key], ct);
        if (existing is null)
        {
            LoggingService.MemberNotFound(key);
            return NotFound();
        }

        delta.Patch(existing);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = existing.RowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        LoggingService.MemberPatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes a Member.
    /// OData route: DELETE /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.DeletingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var member = await context.Members.FindAsync([key], ct);
        if (member is null)
        {
            LoggingService.MemberNotFound(key);
            return NotFound();
        }

        context.Members.Remove(member);
        await context.SaveChangesAsync(ct);

        LoggingService.MemberDeleted(key);
        return NoContent();
    }

    // ── Collection navigation properties ────────────────────────────────

    /// <summary>
    /// Returns LOD cases associated with a specific member.
    /// OData route: GET /odata/Members({key})/LineOfDutyCases
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> GetLineOfDutyCases([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingMemberNavigation(key, "LineOfDutyCases");
        var context = await CreateContextAsync(ct);
        return Ok(context.Cases.AsNoTracking().Where(c => c.MemberId == key));
    }
}
