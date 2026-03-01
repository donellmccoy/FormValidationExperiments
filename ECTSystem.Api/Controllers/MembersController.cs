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
    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of <see cref="MembersController"/>.
    /// </summary>
    /// <param name="contextFactory">The EF Core context factory.</param>
    /// <param name="loggingService">The structured logging service.</param>
    public MembersController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
    {
        _contextFactory = contextFactory;
        _loggingService = loggingService;
    }

    /// <summary>
    /// Returns an IQueryable of Members for OData query composition.
    /// OData route: GET /odata/Members
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _loggingService.QueryingMembers();
        var context = await CreateContextAsync(ct);
        return Ok(context.Members.AsNoTracking());
    }

    /// <summary>
    /// Returns a single Member by key with navigation properties.
    /// OData route: GET /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.RetrievingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var member = await context.Members
            //.Include(m => m.LineOfDutyCases)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == key, ct);

        if (member is null)
        {
            _loggingService.MemberNotFound(key);
            return NotFound();
        }

        return Ok(member);
    }

    /// <summary>
    /// Creates a new Member.
    /// OData route: POST /odata/Members
    /// </summary>
    public async Task<IActionResult> Post([FromBody] Member member, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.MemberInvalidModelState("Post");
            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Members.Add(member);
        await context.SaveChangesAsync(ct);

        _loggingService.MemberCreated(member.Id);
        return Created(member);
    }

    /// <summary>
    /// Fully replaces an existing Member.
    /// OData route: PUT /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] Member update, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.MemberInvalidModelState("Put");
            return BadRequest(ModelState);
        }

        _loggingService.UpdatingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.Members.FindAsync([key], ct);
        if (existing is null)
        {
            _loggingService.MemberNotFound(key);
            return NotFound();
        }

        update.Id = key;
        context.Entry(existing).CurrentValues.SetValues(update);
        await context.SaveChangesAsync(ct);

        _loggingService.MemberUpdated(key);
        return Updated(existing);
    }

    /// <summary>
    /// Partially updates an existing Member.
    /// OData route: PATCH /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<Member> delta, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.MemberInvalidModelState("Patch");
            return BadRequest(ModelState);
        }

        _loggingService.PatchingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.Members.FindAsync([key], ct);
        if (existing is null)
        {
            _loggingService.MemberNotFound(key);
            return NotFound();
        }

        delta.Patch(existing);
        await context.SaveChangesAsync(ct);

        _loggingService.MemberPatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes a Member.
    /// OData route: DELETE /odata/Members({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.DeletingMember(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var member = await context.Members.FindAsync([key], ct);
        if (member is null)
        {
            _loggingService.MemberNotFound(key);
            return NotFound();
        }

        context.Members.Remove(member);
        await context.SaveChangesAsync(ct);

        _loggingService.MemberDeleted(key);
        return NoContent();
    }

    // ── Collection navigation properties ────────────────────────────────

    /// <summary>
    /// Returns LOD cases associated with a specific member.
    /// OData route: GET /odata/Members({key})/LineOfDutyCases
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetLineOfDutyCases([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingMemberNavigation(key, nameof(Member.LineOfDutyCases));
        var context = await CreateContextAsync(ct);
        return Ok(context.Cases.AsNoTracking().Where(c => c.MemberId == key));
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
