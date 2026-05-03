using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
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
        ILoggingService loggingService,
        TimeProvider timeProvider)
        : base(contextFactory, loggingService, timeProvider)
    {
    }

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

            return Problem(title: "Not found", detail: $"No case exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        var etag = $"\"{Convert.ToBase64String(rowVersion)}\"";

        if (Request.Headers.IfNoneMatch.ToString() == etag)
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=0, must-revalidate";

        var isBookmarked = await context.Bookmarks.AnyAsync(b => b.UserId == GetAuthenticatedUserId() && b.LineOfDutyCaseId == key, ct);

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
    public async Task<IActionResult> Post([FromBody] CreateCaseDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            LoggingService.InvalidModelState("Post");
            return ValidationProblem(ModelState);
        }

        var lodCase = CaseDtoMapper.ToEntity(dto);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        lodCase.CaseId = await GenerateCaseIdAsync(context, ct);

        if (lodCase.InitiationDate == default)
        {
            lodCase.InitiationDate = TimeProvider.GetUtcNow().UtcDateTime;
        }

        // Retry loop: CaseId suffix is generated from MAX() and concurrent inserts
        // can race to the same value, violating the unique index on CaseId.
        const int maxRetries = 3;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                // Create the initial workflow state history entry so the case is
                // never persisted without a state. Cases always start at
                // MemberInformationEntry — Draft is a transient client-side state
                // that is never stored in the database.
                var now = TimeProvider.GetUtcNow().UtcDateTime;
                lodCase.WorkflowStateHistories ??= [];
                lodCase.WorkflowStateHistories.Add(new WorkflowStateHistory
                {
                    WorkflowState = WorkflowState.MemberInformationEntry,
                    EnteredDate = now,
                    CreatedDate = now,
                    ModifiedDate = now
                });

                context.Cases.Add(lodCase);

                await context.SaveChangesAsync(ct);

                break;
            }
            catch (DbUpdateException ex) when (attempt < maxRetries && ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                // Detach the failed entity so EF doesn't try to re-insert it
                context.Entry(lodCase).State = EntityState.Detached;

                // Clear history entries added in the failed attempt so they
                // are not duplicated on retry.
                lodCase.WorkflowStateHistories?.Clear();

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
    protected virtual async Task<string> GenerateCaseIdAsync(EctDbContext context, CancellationToken ct)
    {
        var today = TimeProvider.GetUtcNow().UtcDateTime.ToString("yyyyMMdd");
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
    /// Partially updates an existing LOD case using an <see cref="UpdateCaseDto"/>.
    /// The client must supply the current ETag (RowVersion) in the <c>If-Match</c> header
    /// for optimistic concurrency control.
    /// OData route: PATCH /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Patch([FromODataUri] int key, [FromBody] UpdateCaseDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            ModelState.AddModelError("dto", "Request body is required.");
            LoggingService.InvalidModelState("Patch");
            return ValidationProblem(ModelState);
        }

        if (!ModelState.IsValid)
        {
            LoggingService.InvalidModelState("Patch");
            return ValidationProblem(ModelState);
        }

        if (!TryGetIfMatchRowVersion(out var clientRowVersion, out var ifMatchError))
        {
            return ifMatchError!;
        }

        LoggingService.PatchingCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return Problem(title: "Not found", detail: $"No case exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = clientRowVersion;

        CaseDtoMapper.ApplyUpdate(dto, existing);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        var patched = await context.Cases.IncludeWorkflowState().AsNoTracking().FirstAsync(c => c.Id == key, ct);

        // Return updated ETag
        Response.Headers.ETag = $"\"{Convert.ToBase64String(patched.RowVersion)}\"";

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
        if (!TryGetIfMatchRowVersion(out var clientRowVersion, out var ifMatchError))
        {
            return ifMatchError!;
        }

        LoggingService.CheckingOutCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return Problem(title: "Not found", detail: $"No case exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        if (existing.IsCheckedOut)
        {
            LoggingService.CaseAlreadyCheckedOut(key, existing.CheckedOutByName);

            return Problem(title: "Checkout conflict", detail: $"Case {key} is already checked out by {existing.CheckedOutByName}.", statusCode: StatusCodes.Status409Conflict);
        }

        var userId = GetAuthenticatedUserId();
        var userName = User.FindFirstValue(ClaimTypes.Name)
                     ?? User.FindFirstValue(ClaimTypes.Email)
                     ?? userId;

        existing.IsCheckedOut = true;
        existing.CheckedOutBy = userId;
        existing.CheckedOutByName = userName;
        existing.CheckedOutDate = TimeProvider.GetUtcNow().UtcDateTime;

        // Optimistic concurrency — use the client-supplied RowVersion from the If-Match header.
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = clientRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.CaseCheckedOut(key, userName);

        Response.Headers.ETag = $"\"{Convert.ToBase64String(existing.RowVersion)}\"";
        return Ok(existing);
    }

    /// <summary>
    /// Checks in a case, clearing the checkout fields.
    /// OData route: POST /odata/Cases({key})/Checkin
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Checkin([FromODataUri] int key, CancellationToken ct = default)
    {
        if (!TryGetIfMatchRowVersion(out var clientRowVersion, out var ifMatchError))
        {
            return ifMatchError!;
        }

        LoggingService.CheckingInCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var existing = await context.Cases.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.CaseNotFound(key);

            return Problem(title: "Not found", detail: $"No case exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        // Only the user who checked out the case (or an Admin) may check it back in
        var userId = GetAuthenticatedUserId();
        if (existing.CheckedOutBy != userId && !User.IsInRole("Admin"))
        {
            return Problem(
                title: "Forbidden",
                detail: "You can only check in cases that you checked out.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        existing.IsCheckedOut = false;
        existing.CheckedOutBy = string.Empty;
        existing.CheckedOutByName = string.Empty;
        existing.CheckedOutDate = null;

        // Optimistic concurrency — use the client-supplied RowVersion from the If-Match header.
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = clientRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.CaseCheckedIn(key);

        // Return the refreshed entity so the client can update its in-memory copy with the
        // new RowVersion and cleared checkout fields. Mirrors the Checkout action and
        // prevents stale-token 409s on subsequent operations from the same client.
        Response.Headers.ETag = $"\"{Convert.ToBase64String(existing.RowVersion)}\"";
        return Ok(existing);
    }

    /// <summary>
    /// Soft-deletes an LOD case by setting IsDeleted = true.
    /// The global query filter on LineOfDutyCase excludes soft-deleted cases from all subsequent queries.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        if (!TryGetIfMatchRowVersion(out var clientRowVersion, out var ifMatchError))
        {
            return ifMatchError!;
        }

        LoggingService.DeletingCase(key);

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var lodCase = await context.Cases.FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            LoggingService.CaseNotFound(key);

            return Problem(title: "Not found", detail: $"No case exists with ID {key}.", statusCode: StatusCodes.Status404NotFound);
        }

        // Prevent deletion of a case that is currently checked out
        if (lodCase.IsCheckedOut)
        {
            LoggingService.CaseCheckedOutByAnother(key, lodCase.CheckedOutByName);

            return Problem(title: "Checkout conflict", detail: $"Case {key} is currently checked out by {lodCase.CheckedOutByName} and cannot be deleted.", statusCode: StatusCodes.Status409Conflict);
        }

        lodCase.IsDeleted = true;
        lodCase.DeletedAt = TimeProvider.GetUtcNow().UtcDateTime;
        lodCase.DeletedBy = User.Identity?.Name ?? string.Empty;

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(lodCase).Property(e => e.RowVersion).OriginalValue = clientRowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(title: "Concurrency conflict", detail: "The entity was modified by another user. Refresh and retry.", statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.CaseDeleted(key);

        return NoContent();
    }

    // ── Collection-bound OData functions ───────────────────────────────

    /// <summary>
    /// Returns cases bookmarked by the current user.
    /// OData route: GET /odata/Cases/Default.Bookmarked()
    /// </summary>
    [HttpGet]
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
    public async Task<IActionResult> Bookmarked(CancellationToken ct = default)
    {
        LoggingService.QueryingCases();

        var context = await CreateContextAsync(ct);

        var query = context.Cases
            .AsNoTracking()
            .Where(c => context.Bookmarks.Any(b => b.UserId == GetAuthenticatedUserId() && b.LineOfDutyCaseId == c.Id));

        return Ok(query);
    }

    /// <summary>
    /// Returns cases filtered by their current workflow state — the most recent
    /// <see cref="WorkflowStateHistory"/> entry by <c>CreatedDate</c>/<c>Id</c>.
    /// Accepts collections of workflow states in the request body (include/exclude mode).
    /// Standard OData query options (<c>$filter</c>, <c>$orderby</c>, <c>$top</c>, <c>$skip</c>, <c>$count</c>) compose on top.
    /// OData route: POST /odata/Cases/ByCurrentState
    /// </summary>
    [HttpPost]
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
    public async Task<IActionResult> ByCurrentState(ODataActionParameters parameters, CancellationToken ct = default)
    {
        LoggingService.QueryingCases();

        var context = await CreateContextAsync(ct);

        IQueryable<LineOfDutyCase> query = context.Cases.AsNoTracking();

        var includeStates = parameters?.ContainsKey("includeStates") == true
            ? ((IEnumerable<WorkflowState>)parameters["includeStates"]).ToArray()
            : [];

        var excludeStates = parameters?.ContainsKey("excludeStates") == true
            ? ((IEnumerable<WorkflowState>)parameters["excludeStates"]).ToArray()
            : [];

        if (includeStates.Length > 0)
        {
            query = query.WhereCurrentWorkflowStateIn(includeStates);
        }

        if (excludeStates.Length > 0)
        {
            query = query.WhereCurrentWorkflowStateNotIn(excludeStates);
        }

        return Ok(query);
    }

    /// <summary>
    /// Returns cases bookmarked by the current user, filtered by their current workflow state in a single
    /// server-side query. Combines <see cref="Bookmarked"/> + <see cref="ByCurrentState"/> to eliminate the
    /// two-round-trip pattern in <c>BookmarkService.GetBookmarkedCasesByCurrentStateAsync</c>.
    /// OData route: POST /odata/Cases/BookmarkedByCurrentState
    /// </summary>
    [HttpPost]
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
    public async Task<IActionResult> BookmarkedByCurrentState(ODataActionParameters parameters, CancellationToken ct = default)
    {
        LoggingService.QueryingCases();

        var context = await CreateContextAsync(ct);
        var userId = GetAuthenticatedUserId();

        IQueryable<LineOfDutyCase> query = context.Cases
            .AsNoTracking()
            .Where(c => context.Bookmarks.Any(b => b.UserId == userId && b.LineOfDutyCaseId == c.Id));

        var includeStates = parameters?.ContainsKey("includeStates") == true
            ? ((IEnumerable<WorkflowState>)parameters["includeStates"]).ToArray()
            : [];

        var excludeStates = parameters?.ContainsKey("excludeStates") == true
            ? ((IEnumerable<WorkflowState>)parameters["excludeStates"]).ToArray()
            : [];

        if (includeStates.Length > 0)
        {
            query = query.WhereCurrentWorkflowStateIn(includeStates);
        }

        if (excludeStates.Length > 0)
        {
            query = query.WhereCurrentWorkflowStateNotIn(excludeStates);
        }

        return Ok(query);
    }

    // ── Collection navigation properties ────────────────────────────────
    // Convention routing: GET /odata/Cases({key})/{NavigationProperty}
    // Returns IQueryable so OData middleware can apply $filter, $orderby, $top, $skip, $count.

    /// <summary>
    /// Returns documents for a specific case.
    /// OData route: GET /odata/Cases({key})/Documents
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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
