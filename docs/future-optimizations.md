# Future Optimizations Plan

Post-migration optimizations for the `ECTSystem.Web` OData service layer. All changes are client-side only (`ECTSystem.Web`), except where noted.

---

## 1. Eliminate Duplicate Document/Tracking Data Fetches

**Priority:** High  
**Effort:** Medium  
**Files:** `EditCase.razor.cs`, `EditCase.Documents.razor.cs`, `EditCase.Form348.razor.cs`

### Problem

`LoadCaseAsync()` calls `CaseService.GetCaseAsync()` which uses `$expand=...WorkflowStateHistories` — fetching WorkflowStateHistories (~85KB payload). But the expanded data is never assigned to `_trackingData`. When the user clicks the Tracking tab, `OnTabIndexChanged` triggers `_trackingGrid.Reload()` → `LoadTrackingData()` → a second HTTP request for the same data.

Documents follow the same pattern: `OnTabIndexChanged` at `DocumentsTabIndex` triggers `_documentsGrid.Reload()` → `LoadDocumentsData()` → a separate HTTP request to `odata/Cases({id})/Documents`.

### Fix

After `GetCaseAsync()` returns, pre-populate `_documentsData` and `_trackingData` from the already-fetched navigation properties:

```csharp
// In LoadCaseAsync, after _lineOfDutyCase = await CaseService.GetCaseAsync(...)
_documentsData = _lineOfDutyCase.Documents?.AsODataEnumerable();
_documentsCount = _lineOfDutyCase.Documents?.Count ?? 0;

_trackingData = _lineOfDutyCase.WorkflowStateHistories?.AsODataEnumerable();
_trackingCount = _lineOfDutyCase.WorkflowStateHistories?.Count ?? 0;
```

In `LoadDocumentsData` and `LoadTrackingData`, add a guard to skip the HTTP call on first load when data is already populated:

```csharp
// Skip re-fetch on initial tab activation — data was pre-populated from case expand
if (_documentsData is not null && args.Skip is null or 0 && string.IsNullOrEmpty(args.Filter))
{
    return;
}
```

Subsequent interactions (paging, filtering, sorting) continue to fetch server-side as they do today.

### Outcome

- Eliminates 2 redundant HTTP requests on initial case load
- Faster Documents/Tracking tab first-render

---

## 2. Convert DocumentHttpService Queries to DataServiceContext

**Priority:** High  
**Effort:** Low  
**Files:** `Services/DocumentHttpService.cs`

### Problem

Both `GetDocumentsAsync` overloads use raw `HttpClient.GetFromJsonAsync` with manual URL construction (`odata/Cases({id})/Documents?$select=...`). Every other query method in the codebase uses `DataServiceContext` with `AddQueryOption`. This inconsistency means:

- Manual URL string concatenation for `$filter`, `$select`, `$top`, `$skip`, `$orderby`, `$count`
- Requires maintaining `ODataResponse<T>` / `ODataCountResponse<T>` DTOs for deserialization
- Bypasses DataServiceContext change tracking and entity resolution

### Fix

Replace both overloads with `DataServiceContext` queries:

```csharp
// Overload 1: Simple list
public async Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken ct = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    var query = Context.Documents
        .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
        .AddQueryOption("$select", DocumentSelect);

    return await ExecuteQueryAsync(query, ct);
}

// Overload 2: Paged query
public async Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
    int caseId, string filter = null, int? top = null, int? skip = null,
    string orderby = null, bool? count = null, CancellationToken ct = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    var query = Context.Documents
        .AddQueryOption("$filter", CombineFilters($"LineOfDutyCaseId eq {caseId}", filter))
        .AddQueryOption("$select", DocumentSelect);

    if (top.HasValue) query = query.AddQueryOption("$top", top.Value);
    if (skip.HasValue) query = query.AddQueryOption("$skip", skip.Value);
    if (!string.IsNullOrEmpty(orderby)) query = query.AddQueryOption("$orderby", orderby);

    return count == true
        ? await ExecutePagedQueryAsync(query, ct)
        : new ODataServiceResult<LineOfDutyDocument> { Value = await ExecuteQueryAsync(query, ct) };
}
```

### Outcome

- Consistent DataServiceContext pattern across all services
- Eliminates manual URL construction for document queries
- May allow removing `ODataResponse<T>` if no other consumers remain

---

## 3. Server-side `Cases/Bookmarked()` OData Function

**Priority:** Medium  
**Effort:** Medium  
**Files:** `CasesController.cs` (API), `BookmarkHttpService.cs` (Web), `ServiceCollectionExtensions.cs` (API — EDM registration)

### Problem

`GetBookmarkedCasesAsync()` uses a 2-phase query:

1. **Phase 1** — Fetch bookmark IDs: `Context.CaseBookmarks.AddQueryOption("$select", "LineOfDutyCaseId")` → returns list of IDs scoped to current user
2. **Phase 2** — Fetch full cases: `Context.Cases.AddQueryOption("$filter", "Id in (id1,id2,...)")` → returns full case entities

This works but produces 2 HTTP round-trips and builds an `$filter` string with an `in` clause that grows linearly with bookmark count. For users with many bookmarks, the URL could approach max length limits.

### Fix

Add a server-side OData bound collection function `Bookmarked()` on `Cases`:

**API — EDM Registration:**
```csharp
var bookmarked = cases.Collection.Function("Bookmarked").ReturnsCollectionFromEntitySet<LineOfDutyCase>("Cases");
```

**API — Controller:**
```csharp
[HttpGet]
[EnableQuery(MaxTop = 100, PageSize = 50)]
public async Task<IActionResult> Bookmarked(CancellationToken ct)
{
    var userId = User.GetUserId();
    var bookmarkedCaseIds = await _context.CaseBookmarks
        .Where(b => b.UserId == userId)
        .Select(b => b.LineOfDutyCaseId)
        .ToListAsync(ct);

    return Ok(_context.LineOfDutyCases.Where(c => bookmarkedCaseIds.Contains(c.Id)));
}
```

**Client — BookmarkHttpService:**
```csharp
public async Task<ODataServiceResult<LineOfDutyCase>> GetBookmarkedCasesAsync(
    string filter, int? top, int? skip, string orderby, bool? count, CancellationToken ct)
{
    var url = BuildNavigationPropertyUrl("odata/Cases/Bookmarked()", filter, top, skip, orderby, count);
    var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<LineOfDutyCase>>(url, JsonOptions, ct);

    return new ODataServiceResult<LineOfDutyCase>
    {
        Value = response?.Value?.ToList() ?? [],
        Count = response?.Count ?? 0
    };
}
```

### Outcome

- Single HTTP round-trip instead of 2
- No URL length risk from large `in` clauses
- Server composes the query as `IQueryable` — OData middleware handles `$filter`, `$top`, `$skip`, `$orderby` on top

---

## 4. Reduce Initial Case Expand Scope

**Priority:** Medium  
**Effort:** Medium  
**Files:** `CaseHttpService.cs`, `EditCase.razor.cs`

### Problem

`GetCaseAsync()` uses:
```
$expand=Authorities,Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP,Notifications,WorkflowStateHistories
```

This fetches all navigation properties in a single request. Some collections (Notifications, Appeals, WorkflowStateHistories) may contain hundreds of records and aren't needed until the user navigates to those tabs.

### Fix

Split the expand into a "core" expand for initial load and lazy-load the rest:

```csharp
// Core expand — always needed for form rendering
private const string CoreExpand = "Authorities,Member,MEDCON,INCAP";

// Deferred expand — loaded when specific tabs are activated
// Appeals       → loaded when Appeals tab is opened
// Notifications → loaded when Notifications tab is opened  
// WorkflowStateHistories → loaded when Tracking tab is opened (already has LoadTrackingData)
```

Add a `GetCaseAsync` overload or parameter:
```csharp
public Task<LineOfDutyCase> GetCaseAsync(string caseId, string expand = CoreExpand, CancellationToken ct = default);
```

### Outcome

- Smaller initial payload (fewer unnecessary collections)
- Faster initial page render
- Tab-specific data loads on demand

### Risk

Requires verifying that `LoadCaseAsync` (and downstream mapping/state machine) don't depend on the deferred collections at init time.

---

## 5. Strongly-typed LINQ-to-OData Queries

**Priority:** Low  
**Effort:** High  
**Files:** All services, all `RadzenDataGrid` `LoadData` handlers in `EditCase.*`

### Problem

All DataServiceContext queries use `AddQueryOption("$filter", rawString)` with raw OData filter strings from Radzen `DataGrid.LoadData` events:
```csharp
query.AddQueryOption("$filter", args.Filter);  // e.g., "contains(MemberName,'Smith')"
```

This works but provides no compile-time safety — a renamed property won't produce a build error, only a runtime OData 400.

### Fix

Convert to strongly-typed LINQ where expressions:
```csharp
// Instead of:  query.AddQueryOption("$filter", "contains(MemberName,'Smith')")
// Use:         query.Where(c => c.MemberName.Contains("Smith"))
```

This requires:
1. Parsing Radzen's `LoadDataArgs.Filter` string into LINQ predicates (or building a filter expression tree)
2. Mapping Radzen column `Property` names to entity properties
3. A translation layer between `RadzenDataGrid` filter/sort descriptors and LINQ

### Outcome

- Compile-time safety for all OData queries
- Refactoring-safe (property renames caught at build time)
- Better IDE support (IntelliSense, Find All References)

### Risk

High effort — Radzen grids emit raw OData filter strings. Building a translator is non-trivial and may not cover all filter operators. Consider only for new grids or critical queries.

---

## 6. `GetCasesByCurrentStateAsync` DataServiceContext Migration

**Priority:** Low  
**Effort:** Medium  
**Files:** `CaseHttpService.cs`, `ODataServiceBase.cs`

### Problem

`GetCasesByCurrentStateAsync()` uses raw `HttpClient.GetAsync()` to call the OData bound collection function `ByCurrentState(includeStates='...',excludeStates='...')`. The URL is manually constructed:

```csharp
var basePath = $"odata/Cases/ByCurrentState(includeStates='{includeCsv}',excludeStates='{excludeCsv}')";
var url = BuildNavigationPropertyUrl(basePath, filter, top, skip, orderby, count);
var httpResponse = await HttpClient.GetAsync(url, cancellationToken);
```

### Fix

Use `DataServiceContext.CreateFunctionQuery<T>` (available in Microsoft.OData.Client v8.x):

```csharp
public async Task<ODataServiceResult<LineOfDutyCase>> GetCasesByCurrentStateAsync(
    string includeStates, string excludeStates, string filter, int? top, int? skip,
    string orderby, bool? count, CancellationToken ct)
{
    var functionUri = new Uri($"Cases/ByCurrentState(includeStates='{includeStates}',excludeStates='{excludeStates}')", UriKind.Relative);
    var query = Context.CreateFunctionQuery<LineOfDutyCase>(string.Empty, functionUri, false);

    if (!string.IsNullOrEmpty(filter)) query = query.AddQueryOption("$filter", filter);
    if (top.HasValue) query = query.AddQueryOption("$top", top.Value);
    if (skip.HasValue) query = query.AddQueryOption("$skip", skip.Value);
    if (!string.IsNullOrEmpty(orderby)) query = query.AddQueryOption("$orderby", orderby);

    return count == true
        ? await ExecutePagedQueryAsync(query, ct)
        : new ODataServiceResult<LineOfDutyCase> { Value = await ExecuteQueryAsync(query, ct) };
}
```

### Outcome

- Consistent DataServiceContext usage for all queries
- May allow removing `ODataCountResponse<T>` / `ODataResponse<T>` DTOs entirely
- Function result entities participate in DataServiceContext change tracking

### Risk

Need to verify `CreateFunctionQuery<T>` works correctly with Blazor WASM and the `SingleHttpClientFactory` adapter. Test with actual server responses.

---

## 7. Remove Legacy DTO Classes

**Priority:** Low  
**Effort:** Low  
**Files:** `ODataServiceBase.cs`

### Problem

`ODataCountResponse<T>` and `ODataResponse<T>` are remnants from the raw `HttpClient` pattern — they exist solely to deserialize `{"@odata.count": N, "value": [...]}` JSON responses:

```csharp
private sealed class ODataCountResponse<T>
{
    [JsonPropertyName("@odata.count")] public int Count { get; set; }
    [JsonPropertyName("value")] public List<T> Value { get; set; }
}
```

### Fix

After completing optimizations 2 and 6 (converting DocumentHttpService and GetCasesByCurrentStateAsync to DataServiceContext), audit remaining usage. If no raw `HttpClient` queries remain that need these DTOs, delete them.

### Outcome

- Cleaner codebase, fewer intermediate types.

### Dependencies

- Complete optimization 2 (DocumentHttpService)
- Complete optimization 6 (GetCasesByCurrentStateAsync)
- Verify no other consumers remain (search for `ODataCountResponse` and `ODataResponse`)

---

## Execution Order

| Phase | Optimizations | Rationale |
|-------|--------------|-----------|
| **Phase 1** | 1, 2 | High-priority, eliminate duplicate HTTP requests and inconsistencies |
| **Phase 2** | 3, 4 | Medium-priority, reduce round-trips and payload sizes |
| **Phase 3** | 6, 7 | Low-priority, full DataServiceContext consistency and cleanup |
| **Deferred** | 5 | High-effort LINQ migration — consider only when refactoring grids |

---

## Validation

After each optimization:
1. Build: `dotnet build ECTSystem.slnx`
2. Run the app and verify affected functionality in browser
3. Check browser DevTools Network tab — confirm reduced HTTP requests and payload sizes
4. Verify RadzenDataGrid paging, sorting, and filtering still work end-to-end
