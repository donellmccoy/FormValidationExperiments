using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OData.Edm;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;
using System.Text.Json.Serialization;
using ECTSystem.Api.Extensions;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for LOD case CRUD operations.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
[Authorize]
public class CasesController : ODataController
{
    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    public CasesController(
        ILoggingService loggingService,
        IDbContextFactory<EctDbContext> contextFactory)
    {
        _loggingService = loggingService;
        _contextFactory = contextFactory;
    }

    /// <summary>Gets the authenticated user's unique identifier from the JWT claims.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the user is not authenticated.</exception>
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxNodeCount = 500)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _loggingService.QueryingCases();

        var context = await CreateContextAsync(ct);

        return Ok(context.Cases.AsNoTracking());
    }

    /// <summary>
    /// Returns LOD cases bookmarked by the current user as an OData-formatted response.
    /// Route: GET /odata/Cases/Bookmarked
    /// </summary>
    [HttpGet]
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxNodeCount = 500)]
    public async Task<IActionResult> Bookmarked(CancellationToken ct = default)
    {
        _loggingService.QueryingBookmarkedCases();

        var context = await CreateContextAsync(ct);

        var query = context.Cases.AsNoTracking().Where(c => context.CaseBookmarks.Any(b => b.UserId == UserId && b.LineOfDutyCaseId == c.Id));

        return Ok(query);
    }

    /// <summary>
    /// Returns a single LOD case by key with all navigation properties.
    /// OData route: GET /odata/Cases({key})
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.RetrievingCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var lodCase = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            _loggingService.CaseNotFound(key);

            return NotFound();
        }

        return Ok(lodCase);
    }

    /// <summary>
    /// Creates a new LOD case. The server generates the <see cref="LineOfDutyCase.CaseId"/>
    /// in YYYYMMDD-XXX format (001–999 sequential suffix per date).
    /// OData route: POST /odata/Cases
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _loggingService.InvalidModelState("Post");

            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        lodCase.CaseId = await GenerateCaseIdAsync(context, ct);

        context.Cases.Add(lodCase);

        await context.SaveChangesAsync(ct);

        var created = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == lodCase.Id, ct);

        _loggingService.CaseCreated(lodCase.Id);

        return Created(created);
    }

    /// <summary>
    /// Generates a case ID in YYYYMMDD-XXX format where XXX is a sequential
    /// number (001–999) based on existing cases for today's date.
    /// </summary>
    private static async Task<string> GenerateCaseIdAsync(EctDbContext context, CancellationToken ct)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"{today}-";

        var maxSuffix = await context.Cases
            .Where(c => c.CaseId.StartsWith(prefix))
            .Select(c => c.CaseId.Substring(prefix.Length))
            .MaxAsync(ct)
            .ConfigureAwait(false);

        var next = maxSuffix is not null && int.TryParse(maxSuffix, out var current)
            ? current + 1
            : 1;

        return $"{today}-{next:D3}";
    }

    /// <summary>
    /// Partially updates an existing LOD case using OData Delta semantics.
    /// Only the properties present in the request body are applied to the entity.
    /// OData route: PATCH /odata/Cases({key})
    /// </summary>
    /// <remarks>
    /// Do NOT add [FromBody] — OData's own input formatter must handle Delta&lt;T&gt;
    /// deserialization. [FromBody] would route through SystemTextJsonInputFormatter
    /// which cannot construct a Delta instance.
    /// </remarks>
    [EnableQuery]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyCase> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
        {
            _loggingService.InvalidModelState("Patch");

            return BadRequest(ModelState);
        }

        _loggingService.PatchingCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            _loggingService.CaseNotFound(key);

            return NotFound();
        }

        delta.Patch(existing);

        await context.SaveChangesAsync(ct);

        var patched = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == key, ct);

        _loggingService.CasePatched(key);

        return Updated(patched);
    }

    /// <summary>
    /// Atomically transitions a LOD case to a new workflow state and persists the
    /// associated history entries in a single database transaction.
    /// Route: POST /odata/Cases({key})/Transition
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Transition([FromODataUri] int key, [FromBody] CaseTransitionRequest request, CancellationToken ct = default)
    {
        if (request is null || !ModelState.IsValid)
        {
            _loggingService.InvalidModelState("Transition");

            return BadRequest(ModelState);
        }

        _loggingService.TransitioningCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            _loggingService.CaseNotFound(key);

            return NotFound();
        }

        // Apply the workflow state change
        existing.WorkflowState = request.NewWorkflowState;

        // Add history entries
        foreach (var entry in request.HistoryEntries)
        {
            entry.LineOfDutyCaseId = key;
            context.WorkflowStateHistories.Add(entry);
        }

        // Single SaveChangesAsync — both the case update and history entries
        // are committed atomically within EF Core's implicit transaction.
        await context.SaveChangesAsync(ct);

        // Re-fetch with all navigations for a complete response
        var updated = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == key, ct);

        // Extract the saved history entries with their server-assigned IDs
        var savedEntries = await context.WorkflowStateHistories
            .Where(h => h.LineOfDutyCaseId == key)
            .OrderByDescending(h => h.Id)
            .Take(request.HistoryEntries.Count)
            .OrderBy(h => h.Id)
            .AsNoTracking()
            .ToListAsync(ct);

        _loggingService.CaseTransitioned(key, savedEntries.Count);

        return Ok(new CaseTransitionResponse
        {
            Case = updated,
            HistoryEntries = savedEntries
        });
    }

    /// <summary>
    /// Checks out a LOD case so other users see it as read-only.
    /// Route: POST /odata/Cases({key})/CheckOut
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CheckOut([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.CheckingOutCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            _loggingService.CaseNotFound(key);
            return NotFound();
        }

        if (existing.IsCheckedOut)
        {
            if (existing.CheckedOutBy == UserId)
            {
                // Already checked out by this user — return success
                return Ok();
            }

            _loggingService.CaseAlreadyCheckedOut(key, existing.CheckedOutByName);
            return Conflict(new { Message = $"Case is already checked out by {existing.CheckedOutByName}." });
        }

        var userName = User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? UserId;

        existing.IsCheckedOut = true;
        existing.CheckedOutBy = UserId;
        existing.CheckedOutByName = userName;
        existing.CheckedOutDate = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        _loggingService.CaseCheckedOut(key, userName);

        return Ok();
    }

    /// <summary>
    /// Checks in a LOD case so it becomes available for editing again.
    /// Route: POST /odata/Cases({key})/CheckIn
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CheckIn([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.CheckingInCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            _loggingService.CaseNotFound(key);
            return NotFound();
        }

        if (!existing.IsCheckedOut)
        {
            // Not checked out — nothing to do
            return Ok();
        }

        if (existing.CheckedOutBy != UserId)
        {
            _loggingService.CaseCheckedOutByAnother(key, existing.CheckedOutByName);
            return Conflict(new { Message = $"Case is checked out by {existing.CheckedOutByName}. Only that user can check it in." });
        }

        existing.IsCheckedOut = false;
        existing.CheckedOutBy = string.Empty;
        existing.CheckedOutByName = string.Empty;
        existing.CheckedOutDate = null;

        await context.SaveChangesAsync(ct);

        _loggingService.CaseCheckedIn(key);

        return Ok();
    }

    /// <summary>
    /// Batch-upserts authority entries for a LOD case. Existing authorities matching
    /// by Role are updated; new roles are inserted; roles not present in the request
    /// are removed.
    /// OData route: POST /odata/Cases({key})/SaveAuthorities
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> SaveAuthorities([FromODataUri] int key, ODataActionParameters parameters, CancellationToken ct = default)
    {
        if (parameters is null)
            return BadRequest();

        if (!ModelState.IsValid)
        {
            foreach (var entry in ModelState.Where(e => e.Value?.Errors.Count > 0))
            {
                foreach (var error in entry.Value.Errors)
                {
                    if (!string.IsNullOrEmpty(error.ErrorMessage))
                        _loggingService.ModelStatePropertyError("SaveAuthorities", entry.Key, error.ErrorMessage);

                    if (error.Exception is not null)
                        _loggingService.ModelStateExceptionError("SaveAuthorities", entry.Key, error.Exception.Message);
                }
            }

            return BadRequest(ModelState);
        }

        if (!parameters.TryGetValue("Authorities", out var authoritiesObj) || authoritiesObj is not IEnumerable<LineOfDutyAuthority> authorities)
        {
            return BadRequest("Missing or invalid 'Authorities' parameter.");
        }

        var authoritiesList = authorities.ToList();

        _loggingService.SavingAuthorities(key, authoritiesList.Count);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.AnyAsync(c => c.Id == key, ct);

        if (!existing)
        {
            _loggingService.CaseNotFound(key);
            return NotFound();
        }

        var existingAuthorities = await context.Authorities
            .Where(a => a.LineOfDutyCaseId == key)
            .ToListAsync(ct);

        // Remove authorities whose role is not in the incoming list
        var incomingRoles = authoritiesList.Select(a => a.Role).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toRemove = existingAuthorities.Where(a => !incomingRoles.Contains(a.Role)).ToList();
        context.Authorities.RemoveRange(toRemove);

        // Upsert: update existing or add new
        foreach (var incoming in authoritiesList)
        {
            var match = existingAuthorities.FirstOrDefault(
                a => string.Equals(a.Role, incoming.Role, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                match.Name = incoming.Name;
                match.Rank = incoming.Rank;
                match.Title = incoming.Title;
                match.ActionDate = incoming.ActionDate;
                match.Recommendation = incoming.Recommendation;
                match.Comments = incoming.Comments;
            }
            else
            {
                incoming.LineOfDutyCaseId = key;
                incoming.Id = 0;
                context.Authorities.Add(incoming);
            }
        }

        await context.SaveChangesAsync(ct);

        var saved = await context.Authorities
            .Where(a => a.LineOfDutyCaseId == key)
            .AsNoTracking()
            .ToListAsync(ct);

        _loggingService.AuthoritiesSaved(key, saved.Count);

        return Ok(saved);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.DeletingCase(key);

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        var lodCase = await context.Cases.IncludeAllNavigations().FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            _loggingService.CaseNotFound(key);

            return NotFound();
        }

        // ClientCascade handles tracked child collections automatically
        context.Cases.Remove(lodCase);

        // MEDCON/INCAP are already loaded via extensions — remove directly from the tracked entities
        if (lodCase.MEDCON is not null)
        {
            context.MEDCONDetails.Remove(lodCase.MEDCON);
        }

        if (lodCase.INCAP is not null)
        {
            context.INCAPDetails.Remove(lodCase.INCAP);
        }

        await context.SaveChangesAsync(ct);

        _loggingService.CaseDeleted(key);

        return NoContent();
    }

    // ── Collection navigation properties ────────────────────────────────
    // Convention routing: GET /odata/Cases({key})/{NavigationProperty}
    // Returns IQueryable so OData middleware can apply $filter, $orderby, $top, $skip, $count.

    /// <summary>
    /// Returns documents for a specific case.
    /// OData route: GET /odata/Cases({key})/Documents
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetDocuments([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Documents));

        var context = await CreateContextAsync(ct);

        return Ok(context.Documents.AsNoTracking().Where(d => d.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns notifications for a specific case.
    /// OData route: GET /odata/Cases({key})/Notifications
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetNotifications([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Notifications));

        var context = await CreateContextAsync(ct);

        return Ok(context.Notifications.AsNoTracking().Where(n => n.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns workflow state histories for a specific case.
    /// OData route: GET /odata/Cases({key})/WorkflowStateHistories
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetWorkflowStateHistories([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.WorkflowStateHistories));

        var context = await CreateContextAsync(ct);

        return Ok(context.WorkflowStateHistories.AsNoTracking().Where(h => h.LineOfDutyCaseId == key));
    }

    // ── Single-valued navigation properties ─────────────────────────────

    /// <summary>
    /// Returns the member associated with a specific case.
    /// OData route: GET /odata/Cases({key})/Member
    /// </summary>
    [EnableQuery]
    public async Task<SingleResult<Member>> GetMember([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Member));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.Member));
    }

    /// <summary>
    /// Returns the MEDCON detail for a specific case.
    /// OData route: GET /odata/Cases({key})/MEDCON
    /// </summary>
    [EnableQuery]
    public async Task<SingleResult<MEDCONDetail>> GetMEDCON([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.MEDCON));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.MEDCON));
    }

    /// <summary>
    /// Returns the INCAP detail for a specific case.
    /// OData route: GET /odata/Cases({key})/INCAP
    /// </summary>
    [EnableQuery]
    public async Task<SingleResult<INCAPDetails>> GetINCAP([FromODataUri] int key, CancellationToken ct = default)
    {
        _loggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.INCAP));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.INCAP));
    }

    // ── Private helpers ─────────────────────────────────────────────────

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
