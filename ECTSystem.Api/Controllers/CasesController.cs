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
using ECTSystem.Shared.Models;
using ECTSystem.Api.Extensions;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for LOD case CRUD operations.
/// The Radzen DataGrid sends OData-compatible $filter, $orderby, $top, $skip, $count
/// query parameters which the OData middleware translates directly into EF Core LINQ queries.
/// Named "CasesController" to match the OData entity set "Cases" (convention routing).
/// </summary>
[Authorize]
public class CasesController : ODataControllerBase
{
    public CasesController(
        IDbContextFactory<EctDbContext> contextFactory,
        ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>Gets the authenticated user's unique identifier from the JWT claims.</summary>
    /// <exception cref="InvalidOperationException">Thrown when the user is not authenticated.</exception>
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        LoggingService.QueryingCases();

        var context = await CreateContextAsync(ct);

        return Ok(context.Cases.AsNoTracking());
    }

    /// <summary>
    /// Returns a single LOD case by key with all navigation properties.
    /// OData route: GET /odata/Cases({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var lodCase = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            LoggingService.CaseNotFound(key);

            return NotFound();
        }

        return Ok(lodCase);
    }

    /// <summary>
    /// Creates a new LOD case. The server generates the <see cref="LineOfDutyCase.CaseId"/>
    /// in YYYYMMDD-XXX format (001–999 sequential suffix per date).
    /// OData route: POST /odata/Cases
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.InvalidModelState("Post");

            return BadRequest(ModelState);
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        // Over-posting guard: reset server-managed fields
        lodCase.Id = 0;
        lodCase.CreatedBy = string.Empty;
        lodCase.CreatedDate = default;
        lodCase.ModifiedBy = string.Empty;
        lodCase.ModifiedDate = default;

        lodCase.CaseId = await GenerateCaseIdAsync(context, ct);

        // Retry loop: CaseId suffix is generated from MAX() and concurrent inserts
        // can race to the same value, violating the unique index on CaseId.
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                context.Cases.Add(lodCase);

                await context.SaveChangesAsync(ct);

                break;
            }
            catch (DbUpdateException) when (attempt < maxRetries)
            {
                // Detach the failed entity so EF doesn't try to re-insert it
                context.Entry(lodCase).State = EntityState.Detached;

                // Re-generate a new CaseId suffix
                lodCase.CaseId = await GenerateCaseIdAsync(context, ct);
            }
        }

        var created = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == lodCase.Id, ct);

        LoggingService.CaseCreated(lodCase.Id);

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
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyCase> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
        {
            LoggingService.InvalidModelState("Patch");

            return BadRequest(ModelState);
        }

        LoggingService.PatchingCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return NotFound();
        }

        delta.Patch(existing);

        // Server-side enrichment: when IsCheckedOut changes, fill in (or clear)
        // the user-identity fields from the JWT so the client cannot impersonate.
        if (delta.GetChangedPropertyNames().Contains(nameof(LineOfDutyCase.IsCheckedOut)))
        {
            if (existing.IsCheckedOut)
            {
                var userName = User.FindFirstValue(ClaimTypes.Name)
                             ?? User.FindFirstValue(ClaimTypes.Email)
                             ?? UserId;
                existing.CheckedOutBy = UserId;
                existing.CheckedOutByName = userName;
                existing.CheckedOutDate = DateTime.UtcNow;
            }
            else
            {
                existing.CheckedOutBy = string.Empty;
                existing.CheckedOutByName = string.Empty;
                existing.CheckedOutDate = null;
            }
        }

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

        var patched = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == key, ct);

        LoggingService.CasePatched(key);

        return Updated(patched);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.DeletingCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var lodCase = await context.Cases.IncludeAllNavigations().FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            LoggingService.CaseNotFound(key);

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

        LoggingService.CaseDeleted(key);

        return NoContent();
    }

    // ── Collection navigation properties ────────────────────────────────
    // Convention routing: GET /odata/Cases({key})/{NavigationProperty}
    // Returns IQueryable so OData middleware can apply $filter, $orderby, $top, $skip, $count.

    /// <summary>
    /// Returns documents for a specific case.
    /// OData route: GET /odata/Cases({key})/Documents
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> GetDocuments([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Documents));

        var context = await CreateContextAsync(ct);

        return Ok(context.Documents.AsNoTracking().Where(d => d.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns notifications for a specific case.
    /// OData route: GET /odata/Cases({key})/Notifications
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> GetNotifications([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Notifications));

        var context = await CreateContextAsync(ct);

        return Ok(context.Notifications.AsNoTracking().Where(n => n.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns workflow state histories for a specific case.
    /// OData route: GET /odata/Cases({key})/WorkflowStateHistories
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> GetWorkflowStateHistories([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.WorkflowStateHistories));

        var context = await CreateContextAsync(ct);

        return Ok(context.WorkflowStateHistories.AsNoTracking().Where(h => h.LineOfDutyCaseId == key));
    }

    // ── Single-valued navigation properties ─────────────────────────────

    /// <summary>
    /// Returns the member associated with a specific case.
    /// OData route: GET /odata/Cases({key})/Member
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<SingleResult<Member>> GetMember([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Member));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.Member));
    }

    /// <summary>
    /// Returns the MEDCON detail for a specific case.
    /// OData route: GET /odata/Cases({key})/MEDCON
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<SingleResult<MEDCONDetail>> GetMEDCON([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.MEDCON));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.MEDCON));
    }

    /// <summary>
    /// Returns the INCAP detail for a specific case.
    /// OData route: GET /odata/Cases({key})/INCAP
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<SingleResult<INCAPDetails>> GetINCAP([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.QueryingCaseNavigation(key, nameof(LineOfDutyCase.INCAP));

        var context = await CreateContextAsync(ct);

        return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.INCAP));
    }
}
