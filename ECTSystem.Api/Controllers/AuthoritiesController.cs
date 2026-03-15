using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Pure OData controller for LineOfDutyAuthority CRUD operations.
/// OData route prefix: /odata/Authorities
/// </summary>
[Authorize]
public class AuthoritiesController : ODataControllerBase
{
    public AuthoritiesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>
    /// Returns an IQueryable of authorities for OData query composition.
    /// OData route: GET /odata/Authorities
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        var context = await CreateContextAsync(ct);
        return Ok(context.Authorities.AsNoTracking());
    }

    /// <summary>
    /// Returns a single authority by key.
    /// OData route: GET /odata/Authorities({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var authority = await context.Authorities.AsNoTracking().FirstOrDefaultAsync(a => a.Id == key, ct);

        if (authority is null)
            return NotFound();

        return Ok(authority);
    }

    /// <summary>
    /// Creates a new authority entry.
    /// OData route: POST /odata/Authorities
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] LineOfDutyAuthority authority, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        if (authority.LineOfDutyCaseId is null or <= 0)
            return BadRequest("LineOfDutyCaseId is required.");

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        // Over-posting guard: reset server-managed fields
        authority.Id = 0;
        authority.CreatedBy = string.Empty;
        authority.CreatedDate = default;
        authority.ModifiedBy = string.Empty;
        authority.ModifiedDate = default;

        context.Authorities.Add(authority);
        await context.SaveChangesAsync(ct);

        return Created(authority);
    }

    /// <summary>
    /// Partially updates an existing authority using OData Delta semantics.
    /// OData route: PATCH /odata/Authorities({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyAuthority> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
            return BadRequest(ModelState);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Authorities.FindAsync([key], ct);

        if (existing is null)
            return NotFound();

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

        return Updated(existing);
    }

    /// <summary>
    /// Deletes an authority entry.
    /// OData route: DELETE /odata/Authorities({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var authority = await context.Authorities.FindAsync([key], ct);

        if (authority is null)
            return NotFound();

        context.Authorities.Remove(authority);
        await context.SaveChangesAsync(ct);

        return NoContent();
    }
}
