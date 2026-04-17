using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// Pure OData controller for LineOfDutyAuthority CRUD operations.
/// OData route prefix: /odata/Authorities
/// </summary>
[Authorize]
public class AuthoritiesController : ODataControllerBase
{
    public AuthoritiesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService, TimeProvider timeProvider)
        : base(contextFactory, loggingService, timeProvider)
    {
    }

    /// <summary>
    /// Returns an IQueryable of authorities for OData query composition.
    /// OData route: GET /odata/Authorities
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingAuthorities();
        var context = await CreateContextAsync(ct);
        return Ok(context.Authorities.AsNoTracking());
    }

    /// <summary>
    /// Returns a single authority by key.
    /// OData route: GET /odata/Authorities({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingAuthority(key);
        var context = await CreateContextAsync(ct);
        return Ok(SingleResult.Create(context.Authorities.AsNoTracking().Where(a => a.Id == key)));
    }

    /// <summary>
    /// Creates a new authority entry.
    /// OData route: POST /odata/Authorities
    /// </summary>
    [Authorize(Roles = "Admin,CaseManager")]
    public async Task<IActionResult> Post([FromBody] CreateAuthorityDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        LoggingService.CreatingAuthority();

        var authority = AuthorityDtoMapper.ToEntity(dto);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.Authorities.Add(authority);
        await context.SaveChangesAsync(ct);

        LoggingService.AuthorityCreated(authority.Id);
        return Created(authority);
    }

    /// <summary>
    /// Partially updates an existing authority using OData Delta semantics.
    /// OData route: PATCH /odata/Authorities({key})
    /// </summary>
    [Authorize(Roles = "Admin,CaseManager")]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyAuthority> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
            return ValidationProblem(ModelState);

        LoggingService.PatchingAuthority(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Authorities.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.AuthorityNotFound(key);
            return Problem(title: "Not found", detail: $"No authority exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        var originalRowVersion = existing.RowVersion;
        delta.Patch(existing);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.AuthorityPatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes an authority entry.
    /// OData route: DELETE /odata/Authorities({key})
    /// </summary>
    [Authorize(Roles = "Admin,CaseManager")]
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.DeletingAuthority(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Authorities.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.AuthorityNotFound(key);
            return Problem(title: "Not found", detail: $"No authority exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        context.Authorities.Remove(existing);
        await context.SaveChangesAsync(ct);

        LoggingService.AuthorityDeleted(key);
        return NoContent();
    }
}
