using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Shared.Enums;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Mapping;
using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;
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
    /// Returns a single LOD case by key. OData $expand controls which
    /// navigation properties are loaded — only the requested ones are fetched.
    /// OData route: GET /odata/Cases({key})
    /// Supports conditional GET via ETag (RowVersion) — returns 304 when unmodified.
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingCase(key);

        var context = await CreateContextAsync(ct);

        // Lightweight ETag check — only reads RowVersion
        var rowVersion = await context.Cases
            .Where(c => c.Id == key)
            .Select(c => c.RowVersion)
            .FirstOrDefaultAsync(ct);

        if (rowVersion is null)
        {
            LoggingService.CaseNotFound(key);

            return NotFound();
        }

        var etag = $"\"{Convert.ToBase64String(rowVersion)}\"";

        if (Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=0, must-revalidate";

        var isBookmarked = await context.CaseBookmarks.AnyAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == key, ct);

        Response.Headers["X-Case-IsBookmarked"] = isBookmarked.ToString().ToLowerInvariant();

        // Return IQueryable — OData middleware applies $expand/$select from the client request
        var query = context.Cases
            .AsSplitQuery()
            .AsNoTracking()
            .Where(c => c.Id == key);

        return Ok(SingleResult.Create(query));
    }

    /// <summary>
    /// Creates a new LOD case. The server generates the <see cref="LineOfDutyCase.CaseId"/>
    /// in YYYYMMDD-XXX format (001–999 sequential suffix per date).
    /// OData route: POST /odata/Cases
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Post([FromBody] CreateCaseDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.InvalidModelState("Post");

            return BadRequest(ModelState);
        }

        var lodCase = CaseDtoMapper.ToEntity(dto);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

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

        var created = await context.Cases.IncludeWorkflowState().AsNoTracking().FirstAsync(c => c.Id == lodCase.Id, ct);

        LoggingService.CaseCreated(lodCase.Id);

        return Created(created);
    }

    /// <summary>
    /// Generates a case ID in YYYYMMDD-XXX format where XXX is a sequential
    /// number (001–999) based on existing cases for today's date.
    /// Uses UPDLOCK to serialize concurrent suffix generation and prevent races.
    /// </summary>
    private static async Task<string> GenerateCaseIdAsync(EctDbContext context, CancellationToken ct)
    {
        var today = DateTime.UtcNow.ToString("yyyyMMdd");
        var prefix = $"{today}-";

        // UPDLOCK serializes concurrent readers on the same date prefix so two
        // callers cannot both compute the same MAX suffix before either inserts.
        var maxSuffix = await context.Database
            .SqlQueryRaw<string>(
                """
                SELECT MAX(SUBSTRING(CaseId, LEN(@p0) + 1, LEN(CaseId) - LEN(@p0))) AS [Value]
                FROM Cases WITH (UPDLOCK, HOLDLOCK)
                WHERE CaseId LIKE @p0 + '%'
                """,
                prefix)
            .FirstOrDefaultAsync(ct);

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

        var patched = await context.Cases.IncludeWorkflowState().AsNoTracking().FirstAsync(c => c.Id == key, ct);

        LoggingService.CasePatched(key);

        return Updated(patched);
    }

    /// <summary>
    /// Checks out a case, recording the current user's identity.
    /// OData route: POST /odata/Cases({key})/Checkout
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Checkout([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.CheckingOutCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return NotFound();
        }

        if (existing.IsCheckedOut)
        {
            LoggingService.CaseAlreadyCheckedOut(key, existing.CheckedOutByName);

            return Conflict();
        }

        var userName = User.FindFirstValue(ClaimTypes.Name)
                     ?? User.FindFirstValue(ClaimTypes.Email)
                     ?? UserId;

        existing.IsCheckedOut = true;
        existing.CheckedOutBy = UserId;
        existing.CheckedOutByName = userName;
        existing.CheckedOutDate = DateTime.UtcNow;

        await context.SaveChangesAsync(ct);

        LoggingService.CaseCheckedOut(key, userName);

        return NoContent();
    }

    /// <summary>
    /// Checks in a case, clearing the checkout fields.
    /// OData route: POST /odata/Cases({key})/Checkin
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Checkin([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.CheckingInCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return NotFound();
        }

        existing.IsCheckedOut = false;
        existing.CheckedOutBy = string.Empty;
        existing.CheckedOutByName = string.Empty;
        existing.CheckedOutDate = null;

        await context.SaveChangesAsync(ct);

        LoggingService.CaseCheckedIn(key);

        return NoContent();
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

        // Prevent deletion of a case that is currently checked out
        if (lodCase.IsCheckedOut)
        {
            LoggingService.CaseCheckedOutByAnother(key, lodCase.CheckedOutByName);

            return Conflict();
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

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(lodCase).Property(e => e.RowVersion).OriginalValue = lodCase.RowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict();
        }

        LoggingService.CaseDeleted(key);

        return NoContent();
    }

    // ── Collection-bound OData functions ───────────────────────────────

    /// <summary>
    /// Returns cases filtered by their current workflow state — the most recent
    /// <see cref="WorkflowStateHistory"/> entry by <c>CreatedDate</c>/<c>Id</c>.
    /// Accepts comma-separated state names in <paramref name="includeStates"/> (include mode)
    /// or <paramref name="excludeStates"/> (exclude mode). Standard OData query options
    /// (<c>$filter</c>, <c>$orderby</c>, <c>$top</c>, <c>$skip</c>, <c>$count</c>) compose on top.
    /// OData route: GET /odata/Cases/ByCurrentState(includeStates='...',excludeStates='...')
    /// </summary>
    [HttpGet]
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
    public async Task<IActionResult> ByCurrentState(
        [FromODataUri] string includeStates = "",
        [FromODataUri] string excludeStates = "",
        CancellationToken ct = default)
    {
        LoggingService.QueryingCases();

        var context = await CreateContextAsync(ct);

        IQueryable<LineOfDutyCase> query = context.Cases.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(includeStates))
        {
            var states = ParseWorkflowStates(includeStates);
            query = query.WhereCurrentWorkflowStateIn(states);
        }

        if (!string.IsNullOrWhiteSpace(excludeStates))
        {
            var states = ParseWorkflowStates(excludeStates);
            query = query.WhereCurrentWorkflowStateNotIn(states);
        }

        return Ok(query);
    }

    /// <summary>
    /// Parses a comma-separated string of <see cref="WorkflowState"/> names into an array.
    /// Invalid names are silently skipped.
    /// </summary>
    private static WorkflowState[] ParseWorkflowStates(string csv)
    {
        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => Enum.TryParse<WorkflowState>(s, ignoreCase: true, out _))
            .Select(s => Enum.Parse<WorkflowState>(s, ignoreCase: true))
            .ToArray();
    }

    // ── Collection navigation properties ────────────────────────────────
    // Convention routing: GET /odata/Cases({key})/{NavigationProperty}
    // Returns IQueryable so OData middleware can apply $filter, $orderby, $top, $skip, $count.

    /// <summary>
    /// Returns documents for a specific case.
    /// OData route: GET /odata/Cases({key})/Documents
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
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
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
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
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
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
