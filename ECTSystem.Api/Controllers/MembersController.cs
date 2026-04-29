using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Persistence.Data;
using ECTSystem.Api.Logging;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
using System.Text.RegularExpressions;

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

    // ── Collection-bound OData actions ─────────────────────────────────

    /// <summary>
    /// Searches members by free-text input matched against name, rank, unit, service number,
    /// pay-grade aliases (e.g. "E-5" → "Sergeant"), and <see cref="ServiceComponent"/>
    /// names/display names. Returns up to 25 ordered by LastName, FirstName.
    /// OData route: POST /odata/Members/Search
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Search(ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (parameters is null || !parameters.TryGetValue("searchText", out var raw) || raw is not string searchText)
        {
            ModelState.AddModelError("searchText", "searchText is required.");
            LoggingService.MemberInvalidModelState(nameof(Search));
            return ValidationProblem(ModelState);
        }

        var trimmed = searchText.Trim();

        LoggingService.SearchingMembers(trimmed.Length);

        if (string.IsNullOrEmpty(trimmed))
        {
            return Ok(Array.Empty<Member>());
        }

        var lowered = trimmed.ToLowerInvariant();

        // Pay-grade alias matches: substring match on rank-name → list of pay grades (e.g. "Sergeant" → ["E-5","E-6"...]).
        var matchingPayGrades = RankToPayGrade
            .Where(kvp => kvp.Key.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value)
            .Distinct()
            .ToArray();

        // ServiceComponent matches: substring match on enum name OR its space-separated display name.
        var matchingComponents = Enum.GetValues<ServiceComponent>()
            .Where(c =>
                c.ToString().Contains(trimmed, StringComparison.OrdinalIgnoreCase) ||
                Regex.Replace(c.ToString(), @"(\B[A-Z])", " $1").Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var context = await CreateContextAsync(ct);

        var query = context.Members
            .AsNoTracking()
            .Where(m =>
                m.LastName.ToLower().Contains(lowered) ||
                m.FirstName.ToLower().Contains(lowered) ||
                m.Rank.ToLower().Contains(lowered) ||
                m.Unit.ToLower().Contains(lowered) ||
                m.ServiceNumber.ToLower().Contains(lowered) ||
                matchingPayGrades.Contains(m.Rank) ||
                matchingComponents.Contains(m.Component))
            .OrderBy(m => m.LastName)
            .ThenBy(m => m.FirstName)
            .Take(25);

        var results = await query.ToListAsync(ct);

        return Ok(results);
    }

    /// <summary>
    /// Maps human-readable rank names to their pay-grade codes for member search.
    /// Substring matched server-side; e.g. typing "Sergeant" matches ranks E-5 through E-9.
    /// </summary>
    private static readonly Dictionary<string, string[]> RankToPayGrade = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Airman Basic", ["E-1"] },
        { "Airman", ["E-1", "E-2", "E-3"] },
        { "Senior Airman", ["E-4"] },
        { "Sergeant", ["E-5", "E-6", "E-7", "E-8", "E-9"] },
        { "Staff Sergeant", ["E-5"] },
        { "Technical Sergeant", ["E-6"] },
        { "Master Sergeant", ["E-7"] },
        { "Senior Master Sergeant", ["E-8"] },
        { "Chief Master Sergeant", ["E-9"] },
        { "Lieutenant", ["O-1", "O-2", "O-3"] },
        { "Captain", ["O-3"] },
        { "Major", ["O-4"] },
        { "Colonel", ["O-5", "O-6"] },
        { "Lieutenant Colonel", ["O-5"] },
        { "General", ["O-7", "O-8", "O-9", "O-10"] },
        { "Brigadier General", ["O-7"] },
        { "Major General", ["O-8"] },
        { "Lieutenant General", ["O-9"] },
    };
}
