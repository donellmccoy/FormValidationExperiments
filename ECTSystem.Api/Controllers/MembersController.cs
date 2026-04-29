using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Api.Logging;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for Member CRUD operations.
/// Named "MembersController" to match the OData entity set "Members" (convention routing).
/// </summary>
[Authorize]
public class MembersController : ODataControllerBase
{
    public MembersController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService, TimeProvider timeProvider)
        : base(contextFactory, loggingService, timeProvider)
    {
    }

    /// <summary>
    /// Returns an IQueryable of Members for OData query composition.
    /// OData route: GET /odata/Members
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingMember(key);
        var context = await CreateContextAsync(ct);
        return Ok(SingleResult.Create(context.Members.AsNoTracking().Where(m => m.Id == key)));
    }

    /// <summary>
    /// Creates a new Member.
    /// OData route: POST /odata/Members
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Post([FromBody] CreateMemberDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(ms => ms.Value?.Errors.Count > 0)
                .Select(ms => $"{ms.Key}: [{string.Join(", ", ms.Value!.Errors.Select(e => e.ErrorMessage))}]");
            LoggingService.MemberInvalidModelState($"Post â€” {string.Join("; ", errors)}");
            return ValidationProblem(ModelState);
        }

        var member = MemberDtoMapper.ToEntity(dto);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        context.Members.Add(member);
        await context.SaveChangesAsync(ct);

        LoggingService.MemberCreated(member.Id);
        return Created(member);
    }

    /// <summary>
    /// Partially updates an existing Member.
    /// OData route: PATCH /odata/Members({key}).
    /// PATCH is the canonical partial-update verb for Members; PUT is intentionally
    /// not exposed (per Microsoft REST guidelines, expose one update verb to avoid
    /// caller ambiguity — see §2.6 remediation plan).
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<Member> delta, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.MemberInvalidModelState("Patch");
            return ValidationProblem(ModelState);
        }

        LoggingService.PatchingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Members.FindAsync([key], ct);
        if (existing is null)
        {
            LoggingService.MemberNotFound(key);
            return Problem(title: "Not found", detail: $"No member exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
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

        LoggingService.MemberPatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes a Member.
    /// OData route: DELETE /odata/Members({key})
    /// Uses <see cref="EntityFrameworkQueryableExtensions.ExecuteDeleteAsync{T}"/> so the
    /// delete completes in a single round trip and reports a clean 404 when the row was
    /// already removed by another caller (no silent success on stale deletes).
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.DeletingMember(key);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var deleted = await context.Members
            .Where(m => m.Id == key)
            .ExecuteDeleteAsync(ct);

        if (deleted == 0)
        {
            LoggingService.MemberNotFound(key);
            return Problem(title: "Not found", detail: $"No member exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        LoggingService.MemberDeleted(key);
        return NoContent();
    }

    // â”€â”€ Collection navigation properties â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    /// <summary>
    /// Returns LOD cases associated with a specific member.
    /// OData route: GET /odata/Members({key})/LineOfDutyCases
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> GetLineOfDutyCases([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingMemberNavigation(key, "LineOfDutyCases");
        var context = await CreateContextAsync(ct);
        return Ok(context.Cases.AsNoTracking().Where(c => c.MemberId == key));
    }
}
