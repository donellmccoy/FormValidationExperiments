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
    private readonly IApiLogService _log;
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    private readonly ODataQueryContext _bookmarkedQueryContext;

    public CasesController(
        IApiLogService log,
        IEdmModel edmModel,
        IDbContextFactory<EctDbContext> contextFactory)
    {
        _log = log;
        _contextFactory = contextFactory;
        _bookmarkedQueryContext = new ODataQueryContext(edmModel, typeof(LineOfDutyCase), new Microsoft.OData.UriParser.ODataPath());
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("User is not authenticated.");

    /// <summary>
    /// Returns an IQueryable of LOD cases for OData query composition.
    /// The [EnableQuery] attribute lets the OData middleware apply $filter, $orderby,
    /// $top, $skip, and $count automatically against the IQueryable.
    /// </summary>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _log.QueryingCases();
        var context = await CreateContextAsync(ct);
        return Ok(context.Cases.AsNoTracking());
    }

    /// <summary>
    /// Returns LOD cases bookmarked by the current user as an OData-formatted response.
    /// Route: GET /odata/Cases/Bookmarked
    /// Uses ODataQueryOptions to apply $filter/$orderby/$top/$skip/$count against the query.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Bookmarked(CancellationToken ct = default)
    {
        _log.QueryingBookmarkedCases();

        var options = new ODataQueryOptions<LineOfDutyCase>(_bookmarkedQueryContext, Request);
        var countRequested = options.Count?.Value == true;

        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);
            var query = context.Cases
                .AsNoTracking()
                .Where(c => context.CaseBookmarks.Any(b => b.UserId == UserId && b.LineOfDutyCaseId == c.Id));

            // Apply $filter first so @odata.count reflects filtered total (before $top/$skip)
            var filteredQuery = options.Filter?.ApplyTo(query, new ODataQuerySettings()) as IQueryable<LineOfDutyCase> ?? query;
            int? totalCount = countRequested ? await filteredQuery.CountAsync(ct) : null;
            var items = await ((IQueryable<LineOfDutyCase>)options.ApplyTo(query, new ODataQuerySettings { EnsureStableOrdering = true }))
                .ToListAsync(ct);

            return Ok(new BookmarkedCasesResponse { Value = items, Count = totalCount });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Client disconnected — return 499 (nginx convention) or just empty 200 to avoid 500 noise
            return StatusCode(499);
        }
    }

    private sealed class BookmarkedCasesResponse
    {
        [JsonPropertyName("value")]
        public List<LineOfDutyCase> Value { get; init; } = [];

        [JsonPropertyName("@odata.count")]
        public int? Count { get; init; }
    }

    /// <summary>
    /// Returns a single LOD case by key with all navigation properties.
    /// OData route: GET /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.RetrievingCase(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lodCase = await CaseWithIncludes(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == key, ct);

        if (lodCase is null)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        return Ok(lodCase);
    }

    /// <summary>
    /// Creates a new LOD case.
    /// OData route: POST /odata/Cases
    /// </summary>
    public async Task<IActionResult> Post([FromBody] LineOfDutyCase lodCase, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
        {
            _log.InvalidModelState("Post");
            return BadRequest(ModelState);
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Cases.Add(lodCase);
        await context.SaveChangesAsync(ct);

        _log.CaseCreated(lodCase.Id);
        return Created(lodCase);
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
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyCase> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
        {
            _log.InvalidModelState("Patch");
            return BadRequest(ModelState);
        }

        _log.PatchingCase(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var existing = await context.Cases.FindAsync([key], ct);
        if (existing is null)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        delta.Patch(existing);
        await context.SaveChangesAsync(ct);

        _log.CasePatched(key);
        return Updated(existing);
    }

    /// <summary>
    /// Deletes an LOD case and its related entities.
    /// OData route: DELETE /odata/Cases({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.DeletingCase(key);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lodCase = await CaseWithIncludes(context).FirstOrDefaultAsync(c => c.Id == key, ct);
        if (lodCase is null)
        {
            _log.CaseNotFound(key);
            return NotFound();
        }

        // ClientCascade handles tracked child collections automatically
        context.Cases.Remove(lodCase);

        // MEDCON/INCAP are already loaded by CaseWithIncludes — remove directly from the tracked entities
        if (lodCase.MEDCON is not null)
            context.MEDCONDetails.Remove(lodCase.MEDCON);
        if (lodCase.INCAP is not null)
            context.INCAPDetails.Remove(lodCase.INCAP);

        await context.SaveChangesAsync(ct);

        _log.CaseDeleted(key);
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
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Documents));
        var context = await CreateContextAsync(ct);
        return Ok(context.Documents.AsNoTracking().Where(d => d.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns timeline steps for a specific case.
    /// OData route: GET /odata/Cases({key})/TimelineSteps
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetTimelineSteps([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.TimelineSteps));
        var context = await CreateContextAsync(ct);
        return Ok(context.TimelineSteps.AsNoTracking().Where(t => t.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns authorities for a specific case.
    /// OData route: GET /odata/Cases({key})/Authorities
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetAuthorities([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Authorities));
        var context = await CreateContextAsync(ct);
        return Ok(context.Authorities.AsNoTracking().Where(a => a.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns appeals for a specific case.
    /// OData route: GET /odata/Cases({key})/Appeals
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetAppeals([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Appeals));
        var context = await CreateContextAsync(ct);
        return Ok(context.Appeals.AsNoTracking().Where(a => a.LineOfDutyCaseId == key));
    }

    /// <summary>
    /// Returns notifications for a specific case.
    /// OData route: GET /odata/Cases({key})/Notifications
    /// </summary>
    [EnableQuery]
    public async Task<IActionResult> GetNotifications([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Notifications));
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
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.WorkflowStateHistories));
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
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.Member));
        var context = await CreateContextAsync(ct);
        return SingleResult.Create(
            context.Cases.AsNoTracking()
                .Where(c => c.Id == key)
                .Select(c => c.Member));
    }

    /// <summary>
    /// Returns the MEDCON detail for a specific case.
    /// OData route: GET /odata/Cases({key})/MEDCON
    /// </summary>
    [EnableQuery]
    public async Task<SingleResult<MEDCONDetail>> GetMEDCON([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.MEDCON));
        var context = await CreateContextAsync(ct);
        return SingleResult.Create(
            context.Cases.AsNoTracking()
                .Where(c => c.Id == key)
                .Select(c => c.MEDCON));
    }

    /// <summary>
    /// Returns the INCAP detail for a specific case.
    /// OData route: GET /odata/Cases({key})/INCAP
    /// </summary>
    [EnableQuery]
    public async Task<SingleResult<INCAPDetails>> GetINCAP([FromODataUri] int key, CancellationToken ct = default)
    {
        _log.QueryingCaseNavigation(key, nameof(LineOfDutyCase.INCAP));
        var context = await CreateContextAsync(ct);
        return SingleResult.Create(
            context.Cases.AsNoTracking()
                .Where(c => c.Id == key)
                .Select(c => c.INCAP));
    }

    // ── Private helpers ─────────────────────────────────────────────────

    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }

    private static IQueryable<LineOfDutyCase> CaseWithIncludes(EctDbContext context)
    {
        return context.Cases
            .AsSplitQuery()
            .Include(c => c.Documents)
            .Include(c => c.Authorities)
            .Include(c => c.TimelineSteps).ThenInclude(t => t.ResponsibleAuthority)
            .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
            .Include(c => c.Member)
            .Include(c => c.MEDCON)
            .Include(c => c.INCAP)
            .Include(c => c.Notifications)
            .Include(c => c.WorkflowStateHistories);
    }
}
