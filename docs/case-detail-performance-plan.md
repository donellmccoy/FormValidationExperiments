# Case Detail Page — Performance Optimization Plan

Derived from runtime trace analysis of the EditCase page load for LOD case `20260312-001`.

## Trace Summary

| Request | SQL (ms) | HTTP Total (ms) | Notes |
|---------|----------|-----------------|-------|
| `GET Cases?$filter=CaseId eq '...'&$expand=...` | ~741 (8 split queries) | ~741 | Initial case load with all navigations |
| `GET CaseBookmarks/IsBookmarked(caseId=1)` | ~114 | ~114 | Sequential after case load |
| `GET Cases(1)/Documents` | ~721 | ~721 | Duplicate — already in $expand |
| `GET Cases(1)/WorkflowStateHistories` | ~717 | ~717 | Duplicate — already in $expand |
| `GET Cases?$filter=(MemberId eq 167 and Id ne 1)` | ~125 | ~2566 | 2s gap between SQL and HTTP response |

**Total page load: ~4.8s**

---

## Architecture Constraint

> **All OData controllers must be pure.** `DocumentFilesController` is the sole exception (REST-only, binary file I/O).

A "pure" OData controller exposes **only** standard OData convention methods (`Get`, `Post`, `Patch`, `Put`, `Delete`) and navigation property getters (`GetDocuments`, `GetMember`, etc.). No custom actions, functions, or `[HttpGet]`/`[HttpPost]` with non-standard parameter shapes.

### Current Violations

| Controller | Custom Endpoints | Disposition |
|---|---|---|
| **CasesController** | `Bookmarked()`, `Transition()`, `CheckOut()`, `CheckIn()`, `SaveAuthorities()` | Decompose to standard OData CRUD |
| **CaseBookmarksController** | `IsBookmarked()`, `DeleteByCaseId()` | Decompose to standard OData CRUD |
| **WorkflowStateHistoriesController** | `Batch()` | Replace with individual `Post()` calls or OData `$batch` |

### Refactoring Plan

#### R1. CasesController — Remove 5 custom actions

| Action | Pure OData Replacement |
|--------|------------------------|
| `Bookmarked()` | Client queries `GET odata/CaseBookmarks?$filter=...&$expand=Case` on the bookmark entity set. No custom function on Cases needed — OData query composition on `CaseBookmarksController.Get()` replaces it entirely. |
| `Transition()` | Client issues `PATCH odata/Cases({id})` to update `WorkflowState` + `POST odata/WorkflowStateHistories` for each history entry. The state machine logic already lives in the Blazor client (`LineOfDutyStateMachine`); the server just persists the result. The atomic guarantee comes from the client sending the PATCH first, then the history POSTs — if any fail, the client retries. |
| `CheckOut()` | Client issues `PATCH odata/Cases({id})` with `Delta<LineOfDutyCase>` setting `IsCheckedOut = true`, `CheckedOutBy`, `CheckedOutAt`. Standard partial update — no custom action needed. |
| `CheckIn()` | Client issues `PATCH odata/Cases({id})` with `Delta<LineOfDutyCase>` setting `IsCheckedOut = false`, clearing `CheckedOutBy`/`CheckedOutAt`. Standard partial update. |
| `SaveAuthorities()` | Client issues individual `POST odata/Authorities` (create) or `PATCH odata/Authorities({id})` (update) per authority entry via a pure `AuthoritiesController : ODataController`. The current `ODataActionParameters` batch shape is replaced by N standard OData calls. |

After cleanup, `CasesController` retains only: `Get()`, `Get(key)`, `Post()`, `Patch()`, `Delete()`, and navigation property getters.

#### R2. CaseBookmarksController — Eliminate custom functions

| Action | Pure OData Replacement |
|--------|------------------------|
| `IsBookmarked(caseId)` | Client calls `GET odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {id}&$top=1&$count=true` and checks `@odata.count > 0`. The server already applies the `UserId` filter. No custom function needed. |
| `DeleteByCaseId(ODataActionParameters)` | Client queries `GET odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {id}&$top=1` to get the bookmark ID, then issues standard `DELETE odata/CaseBookmarks({bookmarkId})`. Two calls, both pure OData. |

#### R3. WorkflowStateHistoriesController — Remove batch action

| Action | Pure OData Replacement |
|--------|------------------------|
| `Batch(List<WorkflowStateHistory>)` | Client issues individual `POST odata/WorkflowStateHistories` per entry. Typically only 1–2 history entries per transition. If atomicity is needed, use OData `$batch` (`POST odata/$batch` with a changeset). Both approaches use only standard OData conventions. |

#### R4. New AuthoritiesController (pure OData)

`SaveAuthorities()` currently batch-upserts authority records via `ODataActionParameters`. Replace with a new `AuthoritiesController : ODataController` exposing standard `Get()`, `Post()`, `Patch()`, `Delete()`. The client issues individual `POST`/`PATCH` per authority. The `LineOfDutyAuthority` entity is already in the EDM; it just needs its own controller.

### Refactored Controller Map

| Controller | Base Class | Route | Responsibility |
|---|---|---|---|
| `CasesController` | `ODataController` | `odata/Cases` | Pure OData CRUD + nav properties |
| `CaseBookmarksController` | `ODataController` | `odata/CaseBookmarks` | Pure OData CRUD (Get, Post, Delete) |
| `AuthoritiesController` *(new)* | `ODataController` | `odata/Authorities` | Pure OData CRUD (Get, Post, Patch, Delete) |
| `DocumentsController` | `ODataController` | `odata/Documents` | Pure OData query-only (Get) |
| `MembersController` | `ODataController` | `odata/Members` | Pure OData CRUD + nav properties |
| `WorkflowStateHistoriesController` | `ODataController` | `odata/WorkflowStateHistories` | Pure OData CRUD (Get, Post) |
| `DocumentFilesController` | `ControllerBase` | `api/cases` | REST — binary upload/download/delete (sole exception) |

---

## Performance Plan

### Phase 1 — Quick Wins (Low Effort, High Impact)

#### ~~1.1 Remove Duplicate Document/History Fetches~~ — CANCELLED

**Reason:** Architecture constraint — when a case is fetched, all children must always be included to avoid multiple calls. The grids for Documents and WorkflowStateHistories should use the already-loaded data from the case entity rather than making separate navigation property requests. This is a future optimization (make grids bind to `_lineOfDutyCase.Documents` / `.WorkflowStateHistories` instead of issuing separate OData navigation property queries).

---

#### 1.2 Parallelize Bookmark + Previous Cases Loads

**Problem:** `LoadCaseAsync` runs three operations sequentially:
1. `GetCaseAsync` → wait
2. `IsBookmarkedAsync` → wait (~114ms)
3. `LoadPreviousCasesAsync` → wait (~2566ms)

Steps 2 and 3 are independent — both only need `_lineOfDutyCase.Id` / `MemberId`.

**Changes:**

- **`ECTSystem.Web/Pages/EditCase.razor.cs`** — In `LoadCaseAsync`, fire bookmark and previous-cases concurrently after the case loads:
  ```csharp
  // After _lineOfDutyCase is loaded and mapped:
  var bookmarkTask = Task.Run(async () =>
  {
      try { _bookmark.IsBookmarked = await CaseService.IsBookmarkedAsync(_lineOfDutyCase.Id, _cts.Token); }
      catch (Exception ex) { Logger.LogWarning(ex, "Failed to check bookmark status"); }
  });

  var previousCasesTask = LoadPreviousCasesAsync(_lineOfDutyCase.MemberId);

  await Task.WhenAll(bookmarkTask, previousCasesTask);
  ```

**Impact:** Saves ~114ms off the critical path (bookmark runs concurrently with the slower previous-cases call).

**Risk:** Low. Both operations write to independent state fields.

---

### Phase 2 — Medium Effort, High Impact

#### 2.1 Batch Bookmark Checks for Previous Cases Grid

**Problem:** `LoadPreviousCasesData` calls `IsBookmarkedAsync` once per case in a `foreach` loop. If the grid shows 10 cases, that's 10 sequential HTTP round-trips.

**Changes:**

- **`ECTSystem.Web/Services/LineOfDutyCaseHttpService.cs`** — Add `GetBookmarkedCaseIdsAsync(int[] caseIds)` that queries the pure OData endpoint:
  ```csharp
  // GET odata/CaseBookmarks?$filter=LineOfDutyCaseId in ({ids})&$select=LineOfDutyCaseId
  public async Task<HashSet<int>> GetBookmarkedCaseIdsAsync(int[] caseIds, CancellationToken ct)
  {
      var filter = string.Join(",", caseIds);
      var bookmarks = await _client.For<CaseBookmark>()
          .Filter($"LineOfDutyCaseId in ({filter})")
          .Select("LineOfDutyCaseId")
          .FindEntriesAsync(ct);
      return bookmarks.Select(b => b.LineOfDutyCaseId).ToHashSet();
  }
  ```

- **`ECTSystem.Web/Pages/EditCase.razor.cs`** — Replace the per-case loop in `LoadPreviousCasesData` with a single OData query:
  ```csharp
  var ids = _previousCases.Select(c => c.Id).ToArray();
  _previousCasesBookmarkedIds = await CaseService.GetBookmarkedCaseIdsAsync(ids, _cts.Token);
  ```

**Impact:** Replaces N sequential HTTP calls with 1 OData query using `$filter` + `in` operator. For 10 cases, saves ~1–2s.

**Risk:** Low. Uses only standard OData query composition on the existing pure `CaseBookmarksController.Get()`. No custom endpoints needed.

---

#### 2.2 Add `$select` to "Other Cases" Query

**Problem:** The "other cases for same member" query returns full `LineOfDutyCase` entities. The grid only displays summary columns (CaseId, IncidentType, InitiationDate, WorkflowState, etc.). Serializing full entities with all scalar properties contributes to the 2s HTTP header delay.

**Changes:**

- **`ECTSystem.Web/Services/LineOfDutyCaseHttpService.cs`** — Add a `select` parameter to `GetCasesAsync` and apply it to the OData query.

- **`ECTSystem.Web/Pages/EditCase.razor.cs`** — In `LoadPreviousCasesData`, pass `$select` with only the columns the grid needs:
  ```csharp
  var result = await CaseService.GetCasesAsync(
      filter: filter,
      select: "Id,CaseId,IncidentType,InitiationDate,WorkflowState,MemberId,CurrentStatus",
      top: args.Top,
      skip: args.Skip,
      orderby: ...,
      count: true,
      cancellationToken: _cts.Token);
  ```

**Impact:** Reduces serialization and transfer size. May partially address the 2s HTTP delay.

**Risk:** Low. OData `$select` is natively supported. Verify the grid doesn't reference unselected properties.

---

### Phase 3 — Optimization & Tuning

#### 3.1 Evaluate Single-Query Mode for Case Detail

**Problem:** `IncludeAllNavigations` (and the proposed `IncludeEditNavigations`) calls `.AsSplitQuery()`, producing 6–8 sequential SQL queries. For a single-entity fetch by PK, the cartesian product risk is lower because there's only one root row.

**Investigation:**

- Remove `.AsSplitQuery()` from the edit-page include method and benchmark with representative data.
- If collection sizes are small (Authorities < 10, Appeals < 5, Notifications < 20), a single SQL query will be faster than 6 sequential round-trips.
- If cartesian explosion is observed (e.g., 10 Authorities × 20 Notifications = 200 result rows), keep split query.

**Changes:**

- **`ECTSystem.Api/Extensions/LineOfDutyCaseQueryExtensions.cs`** — In `IncludeEditNavigations`, omit `.AsSplitQuery()`:
  ```csharp
  public static IQueryable<LineOfDutyCase> IncludeEditNavigations(this IQueryable<LineOfDutyCase> query)
  {
      return query
          .Include(c => c.Authorities)
          .Include(c => c.Appeals).ThenInclude(a => a.AppellateAuthority)
          .Include(c => c.Member)
          .Include(c => c.MEDCON)
          .Include(c => c.INCAP)
          .Include(c => c.Notifications)
          .Include(c => c.WitnessStatements)
          .Include(c => c.AuditComments);
  }
  ```

  Note: The global `UseQuerySplittingBehavior(SplitQuery)` in `ServiceCollectionExtensions.cs` applies by default, so you must explicitly call `.AsSingleQuery()` to override it:
  ```csharp
  return query
      .AsSingleQuery()
      .Include(c => c.Authorities)
      // ...
  ```

**Impact:** Potentially reduces case load from 6 sequential queries (~500ms) to 1 query (~100ms).

**Risk:** Medium. Requires benchmarking with production-scale data to validate cartesian product size.

---

#### 3.2 Add Composite Index for Previous Cases Query

**Problem:** The query `WHERE MemberId = @p AND Id != @p2 ORDER BY InitiationDate DESC` uses the existing `IX_Cases_MemberId` index but may require a sort operation for the `ORDER BY`.

**Changes:**

- **`ECTSystem.Persistence/Data/Configurations/LineOfDutyCaseConfiguration.cs`** — Add a covering index:
  ```csharp
  builder.HasIndex(e => new { e.MemberId, e.InitiationDate })
         .HasDatabaseName("IX_Cases_MemberId_InitiationDate");
  ```

- Generate and apply a migration.

**Impact:** Marginal — SQL is already 125ms. Eliminates a potential sort operation.

**Risk:** Low. Additive index, no schema change.

---

#### 3.3 Investigate the 2s HTTP Header Delay on "Other Cases"

**Problem:** SQL executes in 125ms but HTTP response headers arrive 2,441ms later. The gap suggests one of:
- Thread-pool starvation (async-over-sync or blocking calls)
- Expensive OData serialization of full `LineOfDutyCase` entities
- Connection pool exhaustion (all 8 split queries hold connections)
- Cold DbContext creation from the pool

**Investigation Steps:**

1. Apply `$select` (Phase 2.2) and re-measure — if the gap shrinks, it's serialization.
2. Check if the "other cases" request is queued behind in-flight split-query connections. The default SQL Server pool is 100 connections, but if split queries are holding 8 connections each across concurrent requests, contention is possible.
3. Add `Stopwatch` timing in the `Get()` controller action around context creation, query execution, and return to identify the slow segment.
4. Profile with `dotnet-trace` or Application Insights to capture the server-side timeline.

---

## Expected Results

| Phase | Target Savings | Cumulative Load Time |
|-------|---------------|---------------------|
| Baseline | — | ~4.8s |
| Phase 1 (remove duplicates + parallelize) | ~1.5s | ~3.3s |
| Phase 2 (batch bookmarks + $select) | ~1.5s | ~1.8s |
| Phase 3 (single query + investigate delay) | ~0.5–1s | ~0.8–1.3s |

## Implementation Order

1. ~~**1.1** Remove duplicate fetches~~ — CANCELLED (all children must be fetched with case)
2. **1.2** Parallelize bookmark + previous cases — trivial change
3. **2.2** Add `$select` to other-cases query — may resolve the 2s delay
4. **2.1** Batch bookmark checks — eliminates N+1 HTTP pattern
5. **R1** Remove 5 custom actions from `CasesController` — decompose to standard OData CRUD
6. **R4** Add `AuthoritiesController` (pure OData) for authority CRUD
7. **R2** Remove custom functions from `CaseBookmarksController` — use OData query composition
8. **R3** Remove `Batch` from `WorkflowStateHistoriesController` — use individual POSTs or OData `$batch`
9. **3.3** Investigate remaining HTTP delay — diagnostic work
10. **3.1** Evaluate single-query mode — requires benchmarking
11. **3.2** Add composite index — marginal improvement
