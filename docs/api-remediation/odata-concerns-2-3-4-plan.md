# Implementation Plan: OData Concerns #2, #3, and #4

> Follows the completed [Concern #1 plan](reduce-includeallnavigations-plan.md) (✅ Done).
> Source: [OData Controller Design Review](odata-controller-design-review.md)

---

## Overview

| # | Concern | Layer | Effort | Mutual Exclusivity | Status |
|---|---------|-------|--------|-------------------|--------|
| 2 | No `$select` on list-page fetch | Client | Low | None — independent | ✅ Plumbing added; `$select` deferred (see note) |
| 3 | `ResponseCache(Duration = 60)` staleness | Server | Low | None — independent | ✅ Done |
| 4 | `IncludeAllNavigations` on single-entity GET | Server | Medium | None — independent | ✅ Done |

All three concerns are **independent** and can be implemented in any order. None are mutually exclusive.

---

## Concern #2 — Add `$select` to Case List Queries ✅

> **Status:** Plumbing complete. `select` parameter added to `ICaseService.GetCasesByCurrentStateAsync` and `CaseHttpService`. `ListSelect` constant defined in `CaseList.razor.cs`. However, `$select` is **not actively passed** from call sites because `CurrentWorkflowState` is computed from `WorkflowStateHistories` navigation — using `$select` without `$expand=WorkflowStateHistories` causes the Status column to show "Draft" for all rows. Revisit when a persisted `CurrentWorkflowStateName` computed column is added to the DB.

### Problem

`CaseList.razor.cs` calls `GetCasesAsync()` and `GetCasesByCurrentStateAsync()` **without `$select`**, fetching all ~90 scalar properties of `LineOfDutyCase` for every row. The grid only displays **9 columns**:

| Column | Property |
|--------|----------|
| Case ID | `CaseId` |
| SSN | `ServiceNumber` |
| Member | `MemberName` |
| Rank | `MemberRank` |
| Unit | `Unit` |
| Status | `CurrentWorkflowState` (derived from `WorkflowStateHistories`) |
| Type | `IncidentType` |
| Incident Date | `IncidentDate` |
| Process | `ProcessType` |

Additional properties needed by list-page logic:
- `Id` — bookmark toggle, navigation
- `IsCheckedOut`, `CheckedOutByName` — lock icon display
- `Component` — may be used in filters

That's **12 properties** out of ~90. Each list-page response currently includes ~78 unused scalar columns per row.

### Assessment

The design review says: _"For the detail page this is acceptable — all properties are needed across the 17 tabs."_ The detail fetch (`GetCaseAsync`) is fine as-is. The concern applies to the **list page** where only a small subset is needed.

### Implementation Steps

#### Step 1: Add `$select` to `GetCasesAsync` call site

**File:** `ECTSystem.Web/Pages/CaseList.razor.cs`

Add a `select` constant and pass it to `GetCasesAsync()`:

```csharp
// Properties needed by the case-list grid columns and UI logic
private const string ListSelect = "Id,CaseId,ServiceNumber,MemberName,MemberRank,Unit,IncidentType,IncidentDate,ProcessType,Component,IsCheckedOut,CheckedOutByName";
```

Update `LoadData()`:

```csharp
var result = await CaseService.GetCasesAsync(
    filter: filter,
    top: args.Top,
    skip: args.Skip,
    orderby: args.OrderBy,
    select: ListSelect,       // ← add this
    count: true,
    cancellationToken: ct);
```

#### Step 2: Add `$select` to `GetCasesByCurrentStateAsync`

**File:** `ECTSystem.Web/Services/CaseHttpService.cs`

Add a `select` parameter to `GetCasesByCurrentStateAsync()`:

```csharp
public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesByCurrentStateAsync(
    ...,
    string? select = null,           // ← add
    CancellationToken cancellationToken = default)
{
    var url = BuildNavigationPropertyUrl(basePath, filter, top, skip, orderby, count, select);  // ← pass
    ...
}
```

Update `ICaseService.cs` interface accordingly.

#### Step 3: Handle `CurrentWorkflowState` derivation

`CurrentWorkflowState` is a **computed property** derived from `WorkflowStateHistories` navigation:

```csharp
public WorkflowState CurrentWorkflowState =>
    WorkflowStateHistories?
        .OrderByDescending(h => h.CreatedDate)
        .ThenByDescending(h => h.Id)
        .Select(h => h.WorkflowState)
        .FirstOrDefault() ?? WorkflowState.Draft;
```

Since the list query doesn't `$expand=WorkflowStateHistories`, this property returns `Draft` for all cases. The **grid already works** because the `ByCurrentState` bound function handles state filtering server-side, and the Status column displays `CurrentWorkflowState.ToDisplayString()`.

**Problem:** Without `$expand=WorkflowStateHistories`, every row's Status column shows "Draft".

**Options:**
1. Add `$expand=WorkflowStateHistories` to list queries — **negates bandwidth savings** (histories can be large)
2. Add a **persisted computed column** `CurrentWorkflowStateName` to the DB — best perf, but requires migration
3. Accept the tradeoff: use `$select` only when state is filtered via `ByCurrentState` (the status is already implicit)

**Recommended:** Option 2 if long-term perf matters; Option 3 as a quick win. For now, if `$select` is used, **omit the Status column** from the `select` list since it requires navigation data. The `ByCurrentState` function's filter already communicates the state.

**Practical approach:** Skip `$select` for the initial implementation if the Status column must always display. The bandwidth savings (~78 fewer scalar columns × 50 rows/page) is moderate. Revisit when a persisted `CurrentWorkflowStateName` column is added.

### Risk

Low. The `$select` parameter is already plumbed through `GetCasesAsync` and `BuildNavigationPropertyUrl`. The main risk is the Status column regression if `WorkflowStateHistories` isn't expanded.

### Tests

- Verify `GetCasesAsync` with and without `$select` parameter
- Verify grid displays all columns correctly
- Verify Status column still shows correct state (or degrade gracefully)

---

## Concern #3 — Tune `ResponseCache` on Collection GET ✅

> **Status:** Done. `[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]` removed from collection GET endpoint.

### Problem

```csharp
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> Get(CancellationToken ct = default)
```

The collection GET endpoint sets a **60-second client-side cache**. When a user:
1. Creates a new case and navigates back to the list → the new case won't appear for up to 60 seconds
2. Forwards a case to the next reviewer → the case still appears in the sender's filtered list
3. Another user checks out a case → the first user's list still shows it as available

The `ByCurrentState` bound function endpoint has **no** `[ResponseCache]` attribute, so it's unaffected.

### Assessment

The review says: _"Safe because the cache is per-client and short-lived, but be aware that newly created/forwarded cases won't appear for up to 60 seconds without a hard refresh."_

This is a **UX issue**, not a data-integrity issue. The tradeoff is between server load (potentially frequent list queries) and data freshness.

### Implementation Steps

#### Step 1: Reduce cache duration

**File:** `ECTSystem.Api/Controllers/CasesController.cs`

Change from 60 seconds to 10 seconds:

```csharp
[ResponseCache(Duration = 10, Location = ResponseCacheLocation.Client)]
```

Rationale: 10 seconds still prevents rapid-fire duplicate requests from page/sort/filter interactions, but reduces maximum staleness to an acceptable window.

#### Step 2: Add cache-busting on client after mutations

**File:** `ECTSystem.Web/Pages/CaseList.razor.cs`

After the user returns from creating or editing a case, explicitly reload the grid. The page already does this for checkout failures:

```csharp
if (_lastArgs is not null)
{
    await LoadData(_lastArgs);
}
```

The browser's HTTP cache will serve the stale response for the first `LoadData` call after navigation. To bust the cache, add a cache-buster query parameter:

Approach — add a `_cacheBuster` nonce to the OData query as a custom query option. However, OData middleware will reject unknown query parameters.

**Better approach:** On the `CaseList` page, detect when the user has just returned from create/edit (via query string `?from=edit` or a static flag) and force a no-cache fetch by adding a `Cache-Control: no-cache` request header.

**File:** `ECTSystem.Web/Services/CaseHttpService.cs`

Add an overload or flag that sends `Cache-Control: no-cache` on the request:

```csharp
public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
    ...,
    bool bypassCache = false,
    CancellationToken cancellationToken = default)
{
    // When bypassCache is true, clone the HttpClient request with no-cache header
    // This overrides the ResponseCache directive for this single request
}
```

**Alternative (simpler):** Remove `[ResponseCache]` entirely. The Blazor WASM client already has request cancellation and debouncing (`_loadCts`). In a single-user client app, the 60s cache provides minimal benefit since the JS fetch API doesn't guarantee HTTP cache hits for programmatic requests through `HttpClient` in Blazor WASM anyway.

### Recommended Approach

**Remove the `[ResponseCache]` attribute.** In a Blazor WASM app, `HttpClient` requests go through the browser's Fetch API. The browser may or may not honor `Cache-Control` response headers for programmatic fetch calls (it depends on the fetch `cache` mode, which HttpClient doesn't control). The existing client-side debouncing (`_loadCts` with cancellation) already prevents rapid duplicate requests. The cache provides marginal benefit and causes real UX confusion.

```csharp
// BEFORE
[EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> Get(CancellationToken ct = default)

// AFTER
[EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500)]
public async Task<IActionResult> Get(CancellationToken ct = default)
```

### Risk

Low. Removing the cache increases server load proportionally to the number of list-page interactions. With `MaxTop = 100` and `PageSize = 50`, each query is bounded. For a small user base (military LOD processing teams), this is negligible. If scaling becomes a concern later, add server-side response caching with proper invalidation rather than client-side cache headers.

### Tests

- Existing controller tests should pass unchanged (response cache is a middleware concern, not a logic concern)
- Manual verification: create a case, navigate back to list, confirm it appears immediately

---

## Concern #4 — Remove `IncludeAllNavigations` from Single-Entity GET ✅

> **Status:** Done. Single-entity GET refactored: lightweight RowVersion-only query for ETag, `SingleResult.Create(query)` return for OData-driven `$expand`, `CreateContextAsync` for proper context lifecycle. Unit test updated for `SingleResult<LineOfDutyCase>` assertion.

### Problem

```csharp
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    var lodCase = await context.Cases
        .IncludeAllNavigations()    // ← 10 explicit .Include() calls
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == key, ct);
    ...
}
```

`IncludeAllNavigations()` eagerly loads **all 10 navigation properties** before OData middleware has a chance to honor `$select` or `$expand`. This means:
- Even if the client sends `$select=Id,CaseId`, the server still runs 11 SQL queries
- The full object graph is always materialized in memory on the server
- OData's `$expand` parameter is redundant — everything is already loaded

### Current Client Request

```
GET /odata/Cases?$filter=Id eq {id}&$top=1&$expand=Authorities,Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP,Notifications,WorkflowStateHistories
```

The client already tells the server exactly which navigation properties it needs via `$expand`. But the server ignores this because `IncludeAllNavigations()` pre-loads everything.

**Missing from client `$expand`:** `Documents`, `WitnessStatements`, `AuditComments` — these 3 are loaded server-side but **not requested** by the client. They're loaded in separate grid requests later.

### Target Architecture

Let OData middleware handle `$expand` natively. The `[EnableQuery]` attribute can translate `$expand` to EF `.Include()` calls automatically when the endpoint returns `IQueryable` (or `SingleResult`).

### Implementation Steps

#### Step 1: Convert single-entity GET to return `SingleResult<T>`

**File:** `ECTSystem.Api/Controllers/CasesController.cs`

Replace the explicit `FirstOrDefaultAsync` + `IncludeAllNavigations()` with an `IQueryable` that OData middleware can compose on:

```csharp
// BEFORE
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var lodCase = await context.Cases
        .IncludeAllNavigations()
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Id == key, ct);

    if (lodCase is null) return NotFound();

    // ETag check...
    // Bookmark check...

    return Ok(lodCase);
}

// AFTER
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);

    var query = context.Cases
        .AsSplitQuery()
        .AsNoTracking()
        .Where(c => c.Id == key);

    var lodCase = await query.FirstOrDefaultAsync(ct);

    if (lodCase is null)
    {
        LoggingService.CaseNotFound(key);
        return NotFound();
    }

    // ETag check (still works — RowVersion is a scalar property)
    var etag = $"\"{Convert.ToBase64String(lodCase.RowVersion)}\"";
    if (Request.Headers.IfNoneMatch.ToString() == etag)
    {
        return StatusCode(StatusCodes.Status304NotModified);
    }

    Response.Headers.ETag = etag;

    // Bookmark check...

    // Return IQueryable — OData middleware applies $expand, $select
    return Ok(SingleResult.Create(query));
}
```

**Key change:** The endpoint returns `SingleResult.Create(query)` — an `IQueryable` filtered to a single entity. OData middleware applies `$expand` from the client request, translating it to EF `.Include()` calls. Only the requested navigation properties are loaded.

#### Step 2: Handle the ETag/304 challenge

The challenge with returning `SingleResult<IQueryable>` is that the ETag check requires materializing the entity first (to read `RowVersion`), but OData middleware expects an `IQueryable` to compose on.

**Two queries approach:**
1. First query: `SELECT RowVersion FROM Cases WHERE Id = @key` (minimal) for ETag check
2. If not 304: return `SingleResult.Create(query)` for OData to compose

```csharp
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    LoggingService.RetrievingCase(key);

    await using var context = await ContextFactory.CreateDbContextAsync(ct);

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

    // Bookmark header
    var userId = UserId;
    var isBookmarked = await context.CaseBookmarks
        .AnyAsync(b => b.CaseId == key && b.UserId == userId, ct);
    Response.Headers["X-Case-IsBookmarked"] = isBookmarked.ToString();

    // Return IQueryable for OData to apply $expand/$select
    var query = context.Cases
        .AsSplitQuery()
        .AsNoTracking()
        .Where(c => c.Id == key);

    return Ok(SingleResult.Create(query));
}
```

**SQL improvement:**
- **Before:** 11 queries (1 base + 10 includes) always
- **If 304:** 1 query (just RowVersion) → 304 response, no body
- **If 200:** 1 RowVersion query + 1 base + N includes (only for requested `$expand` properties)
  - Current client requests 7 navigations → 8 queries (down from 11)
  - Server no longer loads Documents, WitnessStatements, AuditComments unless client requests them

#### Step 3: Verify client `$expand` coverage

The client `FullExpand` constant in `CaseHttpService.cs`:

```
Authorities,Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP,Notifications,WorkflowStateHistories
```

This requests **7 of 10** navigation properties. The 3 omitted:
- `Documents` — loaded separately by `LoadDocumentsData()` via `Cases({id})/Documents`
- `WitnessStatements` — loaded separately (if implemented)
- `AuditComments` — loaded separately (if implemented)

After this change, these 3 will **correctly** not be loaded on the initial fetch. No client changes needed — the client already uses separate endpoints for these.

#### Step 4: Verify `AsSplitQuery` behavior with OData `$expand`

When OData middleware translates `$expand=Authorities,Member,...` into EF `.Include()` calls on the `IQueryable`, the `.AsSplitQuery()` we applied ensures each include is a separate SQL query (avoiding cartesian products). Verify this works correctly with `SingleResult`.

**Test:** Run the app, open a case, check SQL queries in EF Core logging. Should see separate SELECT per expanded navigation property.

#### Step 5: Update `GetCaseAsync` client method (if needed)

**File:** `ECTSystem.Web/Services/CaseHttpService.cs`

The current client fetches via:
```
GET /odata/Cases?$filter=Id eq {id}&$top=1&$expand=...
```

This hits the **collection GET** endpoint (which returns `IQueryable` without `IncludeAllNavigations`), not the single-entity GET. So the OData middleware already handles `$expand` natively for this path.

**Check:** Does the client ever call the single-entity GET (`/odata/Cases({id})`) directly? Search for `Cases(` pattern in client code.

If the client only uses the `$filter=Id eq {id}` pattern (which routes to the collection endpoint), then Concern #4 is **already resolved** for the primary usage path. The single-entity GET cleanup is still valuable for direct URL access and future API consumers.

### Risk

**Medium.** Changing how `$expand` is applied affects query generation. Key risks:
1. OData middleware may not translate `$expand` with nested expansion (`Appeals($expand=AppellateAuthority)`) correctly through `AsSplitQuery` — needs testing
2. `SingleResult` must be used correctly with ETag handling — the two-query approach avoids materializing the full entity graph for 304 responses
3. Third-party API consumers (if any) that rely on the single-entity GET always returning full navigation data will break

### Tests

- Existing controller tests for single GET should verify navigation properties are returned when `$expand` is specified
- Add a test: GET without `$expand` should return scalar properties only
- Add a test: ETag 304 response should not trigger navigation property queries
- Manual: verify all 17 EditCase tabs load data correctly

---

## Implementation Order

| Priority | Concern | Rationale |
|----------|---------|-----------|
| 1 | **#3** — Remove `ResponseCache` | 1-line change, immediate UX improvement, no risk |
| 2 | **#2** — Add `$select` to list queries | Low risk, moderate bandwidth savings (~80% fewer columns per row) |
| 3 | **#4** — Remove `IncludeAllNavigations` from GET | Most impactful but highest risk; needs thorough testing |

---

## Summary of Changes

| File | Concern | Change | Status |
|------|---------|--------|--------|
| `CasesController.cs` | #3 | Remove `[ResponseCache(Duration = 60, ...)]` from collection GET | ✅ |
| `CasesController.cs` | #4 | Replace `IncludeAllNavigations()` with `SingleResult` + OData-driven `$expand` in single GET | ✅ |
| `CaseList.razor.cs` | #2 | Define `ListSelect` constant; pass `select: ListSelect` to `GetCasesAsync()` | ✅ Constant added; passing deferred |
| `CaseHttpService.cs` | #2 | Add `select` param to `GetCasesByCurrentStateAsync()` | ✅ |
| `ICaseService.cs` | #2 | Update interface signature for `GetCasesByCurrentStateAsync()` | ✅ |
| `CasesControllerTests.cs` | #4 | Update `GetByKey` test assertion for `SingleResult<LineOfDutyCase>` | ✅ |
