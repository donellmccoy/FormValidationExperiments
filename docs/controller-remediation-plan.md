# Controller Remediation Plan

Consolidated implementation plan derived from the characterization reviews of all six OData/API controllers: **Authorities**, **Bookmarks**, **Cases**, **Documents**, **Members**, **UserController**, and **WorkflowStateHistory**.

## Current Status

| Metric | Count |
|--------|-------|
| **Total items (Phases 1–8)** | 30 |
| **Completed** ✅ | 30 |
| **Not done** ❌ | 0 |
| **Partial** ⏳ | 0 |
| **Remaining work** | None |
| **Deferred (D.1–D.6)** | 6 — unchanged, future work |

---

## Phase 1 — Critical Security Fixes ✅

**Goal:** Close authentication bypasses and remove debug code. Zero-risk regressions; changes are additive security hardening.

### 1.1 ~~Re-enable `[Authorize]` on CasesController~~ ✅

- **File:** `Controllers/CasesController.cs`
- **Action:** Uncomment `[Authorize]` attribute on the class.
- **Source:** Cases characterization, Weakness #1.
- **Risk if skipped:** Any anonymous user can CRUD all cases, checkout/checkin, and access all case data.

### 1.2 ~~Re-enable `[Authorize]` on UserController~~ ✅

- **File:** `Controllers/UserController.cs`
- **Action:** Uncomment `[Authorize]` attribute on the class.
- **Source:** User characterization, Weakness #1.
- **Risk if skipped:** Anonymous enumeration of user IDs and names; identity endpoint returns `"test-user-id"` for everyone.

### 1.3 ~~Centralize `GetAuthenticatedUserId()` in `ODataControllerBase` — Remove all `"test-user-id"` fallbacks~~ ✅

- **Files:** `Controllers/ODataControllerBase.cs`, `Controllers/CasesController.cs`, `Controllers/BookmarksController.cs`, `Controllers/UserController.cs`
- **Action:**
  1. Add a shared `GetAuthenticatedUserId()` method to `ODataControllerBase` that throws `UnauthorizedAccessException` when the `NameIdentifier` claim is missing.
  2. Replace the private `GetUserId()` methods in `CasesController`, `BookmarksController`, and the inline claim access in `UserController` with calls to the centralized method.
  3. For `UserController` (which inherits `ControllerBase`, not `ODataControllerBase`), either add a shared base or duplicate the same throw-on-missing pattern.
- **Source:** Bookmarks #1, Cases #3, User #2.
- **Risk if skipped:** Cross-user data leakage, unauthorized case checkout, bookmark pollution when auth is misconfigured.

### 1.4 ~~Remove `Console.WriteLine` in CasesController~~ ✅

- **File:** `Controllers/CasesController.cs`
- **Action:** Delete the `Console.WriteLine("USER ID CLAIM IS: " + ...)` line in `GetUserId()`.
- **Source:** Cases characterization, Weakness #2.
- **Risk if skipped:** User identity claims written to stdout in production.

---

## Phase 2 — Data Integrity & Concurrency Fixes ✅

**Goal:** Fix broken or missing optimistic concurrency. These are correctness bugs that can cause silent data loss under concurrent access.

### 2.1 ~~Fix MembersController PATCH RowVersion bug~~ ✅

- **File:** `Controllers/MembersController.cs`
- **Action:** Capture `existing.RowVersion` **before** calling `delta.Patch(existing)`, then use the captured value for `OriginalValue`. Currently the code sets `OriginalValue` after `Patch()` has already overwritten `RowVersion`, so the concurrency check always passes.
- **Source:** Members characterization, Weakness #3.
- **Priority:** Highest in this phase — the concurrency check is silently disabled.

```csharp
// BEFORE (broken):
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = existing.RowVersion;

// AFTER (fixed):
var originalRowVersion = existing.RowVersion;
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;
```

### 2.2 ~~Add concurrency control to WorkflowStateHistoryController PATCH~~ ✅

- **Files:** `Models/WorkflowStateHistory.cs`, `Controllers/WorkflowStateHistoryController.cs`, + EF migration
- **Action:**
  1. Add `[Timestamp] public byte[] RowVersion { get; set; }` to `WorkflowStateHistory` if not already present.
  2. Generate and apply an EF Core migration to add the column.
  3. In the PATCH method, capture `existing.RowVersion` before `delta.Patch()`, set `OriginalValue`, and catch `DbUpdateConcurrencyException` → `409 Conflict`.
- **Source:** WorkflowStateHistory characterization, Weakness #1.
- **Risk if skipped:** Concurrent PATCH requests silently overwrite each other (last-write-wins on audit trail entries).
- **Status:** Done. `WorkflowStateHistory` inherits `AuditableEntity` which already has `[Timestamp] RowVersion`. PATCH captures `originalRowVersion` before `delta.Patch()` and handles `DbUpdateConcurrencyException`.

### ~~2.3 Restrict WorkflowStateHistory PATCH to `ExitDate` only~~ ✅

- **File:** `Controllers/WorkflowStateHistoryController.cs`
- **Action:** After applying the delta, validate that `delta.GetChangedPropertyNames()` contains only `ExitDate`. Return `400 Bad Request` if any other property (especially `WorkflowState`) is included.
- **Source:** WorkflowStateHistory characterization, Weakness #4.
- **Risk if skipped:** Unguarded mutation path for audit fields that should be immutable after creation.
- **Status:** Done. PATCH method now validates `delta.GetChangedPropertyNames()` against an allowed set of `{ "ExitDate" }` before applying the delta. Returns `400 Bad Request` with a Problem Details response if any other property is included.

### 2.4 ~~Add concurrency control to CasesController Checkout/Checkin~~ ✅

- **File:** `Controllers/CasesController.cs`, `Extensions/ServiceCollectionExtensions.cs`, `ECTSystem.Web/Services/CaseService.cs`, `ECTSystem.Web/Services/Interfaces/ICaseService.cs`, `ECTSystem.Shared/ViewModels/CaseListItemViewModel.cs`, `ECTSystem.Shared/Mapping/LineOfDutyCaseMapper.cs`
- **Action:** Accept optional `RowVersion` parameter (via `ODataActionParameters`) in both `Checkout` and `Checkin` actions. Set `OriginalValue` before `SaveChangesAsync` and catch `DbUpdateConcurrencyException` → `409 Conflict`. Updated EDM model to define RowVersion parameter on both actions. Client sends RowVersion via `HttpClient.PostAsJsonAsync`. Added `RowVersion` to `CaseListItemViewModel` and its mapper. Updated all call sites in `EditCase`, `CaseList`, and `MyBookmarks` pages.
- **Source:** Cases characterization, Weakness #5.
- **Risk if skipped:** TOCTOU race condition — two concurrent checkout requests both succeed.
- **Status:** Complete. Both Checkout and Checkin accept client-supplied RowVersion for optimistic concurrency, falling back to DB-loaded RowVersion when not supplied (backward-compatible).

---

## Phase 3 — OData Compliance: `SingleResult` on Single-Entity GETs ✅

**Goal:** Enable server-side `$select`/`$expand` composition on single-entity queries by returning deferred `IQueryable` instead of eagerly materialized entities.

### 3.1 ~~AuthoritiesController `Get(key)` → `SingleResult.Create()`~~ ✅

- **File:** `Controllers/AuthoritiesController.cs`
- **Action:** Replace `FirstOrDefaultAsync` with a deferred `IQueryable` wrapped in `SingleResult.Create()`. Use `CreateContextAsync()` to ensure the context lives through serialization.
- **Source:** Authorities characterization, Weakness #1.

### 3.2 ~~MembersController `Get(key)` → `SingleResult.Create()`~~ ✅

- **File:** `Controllers/MembersController.cs`
- **Action:** Same pattern as 3.1. Replace `FirstOrDefaultAsync` with `SingleResult.Create()`.
- **Source:** Members characterization, Weakness #1.

### 3.3 ~~WorkflowStateHistoryController `Get(key)` → `SingleResult.Create()`~~ ✅

- **File:** `Controllers/WorkflowStateHistoryController.cs`
- **Action:** Same pattern as 3.1. Replace `FirstOrDefaultAsync` with `SingleResult.Create()`.
- **Source:** WorkflowStateHistory characterization, Weakness #2.

**Common pattern for all three:**

```csharp
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    var context = await CreateContextAsync(ct);
    var query = context.EntitySet.AsNoTracking().Where(e => e.Id == key);
    return Ok(SingleResult.Create(query));
}
```

---

## Phase 4 — Performance Improvements ✅

**Goal:** Eliminate unnecessary database round trips and N+1 queries.

### 4.1 ~~AuthoritiesController Delete → `ExecuteDeleteAsync`~~ ✅

- **File:** `Controllers/AuthoritiesController.cs`
- **Action:** Replace the two-roundtrip `FindAsync` + `Remove` + `SaveChangesAsync` with a single `ExecuteDeleteAsync`.
- **Source:** Authorities characterization, Weakness #3.

```csharp
var deleted = await context.Authorities
    .Where(a => a.Id == key)
    .ExecuteDeleteAsync(ct);
return deleted == 0 ? NotFound() : NoContent();
```

### 4.2 ~~MembersController Delete → `ExecuteDeleteAsync`~~ ✅

- **File:** `Controllers/MembersController.cs`
- **Action:** Same pattern as 4.1.
- **Source:** Members characterization, Weakness #5.

### 4.3 ~~UserController `LookupUsers` → Batch query~~ ✅

- **File:** `Controllers/UserController.cs`
- **Action:** Replace the sequential `foreach` + `FindByIdAsync` loop with a single `Where(u => ids.Contains(u.Id))` query.
- **Source:** User characterization, Weakness #3.
- **Status:** Done. Uses `userManager.Users.Where(u => distinctIds.Contains(u.Id))` with server-side projection and `ToDictionaryAsync` for a single `WHERE IN` batch query. Falls back to the raw ID for any IDs not found.

```csharp
var distinctIds = ids.Distinct().ToList();
var users = await userManager.Users
    .Where(u => distinctIds.Contains(u.Id))
    .Select(u => new { u.Id, Name = u.UserName ?? u.Email ?? u.Id })
    .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

foreach (var id in distinctIds)
    users.TryAdd(id, id);
```

### 4.4 ~~UserController `LookupUsers` → Add input validation cap~~ ✅

- **File:** `Controllers/UserController.cs`
- **Action:** Add a maximum length check on the `ids` array (e.g., 50). Return `400 Bad Request` if exceeded.
- **Source:** User characterization, Weakness #4.

### 4.5 ~~UserController `LookupUsers` → Add `CancellationToken`~~ ✅

- **File:** `Controllers/UserController.cs`
- **Action:** Add `CancellationToken ct = default` parameter to `LookupUsers`.
- **Source:** User characterization, Weakness #6.

### 4.6 ~~Dashboard → Batch 3 `ByCurrentState` queries via OData `$batch`~~ ✅

- **Files:** `Services/Interfaces/ICaseService.cs`, `Services/CaseService.cs`, `Pages/Dashboard.razor.cs`
- **Action:**
  1. Add `BuildCasesByCurrentStateQuery()` to `ICaseService`/`CaseService` — builds a `DataServiceQuery<LineOfDutyCase>` for the `ByCurrentState` bound function without executing.
  2. Add `ExecuteBatchQueriesAsync()` — executes an array of `DataServiceQuery<LineOfDutyCase>` in a single OData `$batch` request via `Context.ExecuteBatchAsync`, returning ordered `ODataServiceResult<T>[]` with counts.
  3. Rewrite `Dashboard.LoadDashboardDataAsync()` to build 3 queries (actionRequired, myActiveCases, completedCases) and execute them in one `$batch` call, concurrent with the bookmarks request.
- **Source:** Batching analysis — Dashboard was the highest-impact target (3 independent `ByCurrentState` calls).
- **Impact:** Reduces Dashboard load from 4 HTTP requests to 2 (1 bookmarks + 1 `$batch` containing 3 sub-requests).

### 4.7 ~~BookmarkService → Use server-side `Bookmarked()` OData function~~ ✅

- **Files:** `Extensions/ServiceCollectionExtensions.cs`, `Services/BookmarkService.cs`
- **Action:**
  1. Register `Bookmarked` and `ByCurrentState` as `EdmFunction` (bound, composable) in the client `BuildClientEdmModel()`.
  2. Rewrite `GetBookmarkedCasesAsync()` to use `Context.CreateFunctionQuery<LineOfDutyCase>("Cases", "Default.Bookmarked", false)` — single server-side query replaces the old 2-step pattern (get bookmark IDs → filter cases).
  3. Update `GetBookmarkedCasesByCurrentStateAsync()` to use `Bookmarked()` with `$select=Id` in step 1, then `ByCurrentState` with ID filter in step 2.
- **Source:** Batching analysis — the 2-step bookmark pattern was the second-highest-impact optimization.
- **Impact:** `GetBookmarkedCasesAsync` reduced from 2 HTTP requests to 1. `GetBookmarkedCasesByCurrentStateAsync` retains 2 requests but the first is now a lightweight function call instead of a full collection fetch.

---

## Phase 5 — Cache Policy Corrections ✅

**Goal:** Prevent stale data on actively mutating endpoints.

### 5.1 ~~WorkflowStateHistoryController → `NoStore` on both GETs~~ ✅

- **File:** `Controllers/WorkflowStateHistoryController.cs`
- **Action:** Change `[ResponseCache(Duration = 60)]` to `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` on both `Get()` and `Get(key)`.
- **Source:** WorkflowStateHistory characterization, Weakness #3.
- **Impact:** Workflow sidebar will always show current state instead of up-to-60-second stale data during active transitions.

### 5.2 ~~MembersController navigation `GetLineOfDutyCases` → `NoStore`~~ ✅

- **File:** `Controllers/MembersController.cs`
- **Action:** Change `[ResponseCache(Duration = 60)]` to `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` on `GetLineOfDutyCases`.
- **Source:** Members characterization, Weakness #7.

### 5.3 ~~CasesController navigation endpoints → `NoStore` for mutable collections~~ ✅

- **File:** `Controllers/CasesController.cs`
- **Action:** Change `[ResponseCache(Duration = 60)]` to `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` on `GetDocuments`, `GetNotifications`, and `GetWorkflowStateHistories` navigation endpoints.
- **Source:** Cases characterization, Weakness #10.
- **Note:** Keep `Duration = 60` on `GetMember`, `GetMEDCON`, `GetINCAP` if those are relatively stable during a session.

---

## Phase 6 — Observability: Structured Logging ✅

**Goal:** Ensure all controller operations have structured logging for auditability and troubleshooting.

### 6.1 ~~AuthoritiesController → Add `ILoggingService` calls~~ ✅

- **Files:** `Controllers/AuthoritiesController.cs`, `Logging/ILoggingService.cs` (add method definitions), `Logging/LoggingService.cs` (add implementations)
- **Action:** Add logging methods and calls for all five operations: `QueryingAuthorities`, `RetrievingAuthority(key)`, `AuthorityCreated(id)`, `PatchingAuthority(key)`, `DeletingAuthority(key)`.
- **Source:** Authorities characterization, Weakness #5.

### 6.2 ~~BookmarksController → Add logging on POST and DELETE~~ ✅

- **Files:** `Controllers/BookmarksController.cs`, `Logging/ILoggingService.cs`, `Logging/LoggingService.cs`
- **Action:** Add `BookmarkCreated(id, caseId)` and `BookmarkDeleted(id)` logging calls.
- **Source:** Bookmarks characterization, Weakness #5.

### 6.3 ~~WorkflowStateHistoryController → Add logging on GET endpoints~~ ✅

- **Files:** `Controllers/WorkflowStateHistoryController.cs`, `Logging/ILoggingService.cs`, `Logging/LoggingService.cs`
- **Action:** Add `QueryingWorkflowStateHistory()` and `RetrievingWorkflowStateHistoryEntry(key)` logging calls.
- **Source:** WorkflowStateHistory characterization, Weakness #5.

---

## Phase 7 — Standardize Error Responses (RFC 9457 Problem Details) ✅

**Goal:** Replace bare status code returns with structured `Problem()` responses across all controllers for machine-parseable error bodies.

### 7.1 ~~Register `AddProblemDetails()` in DI~~ ✅

- **File:** `Program.cs`
- **Action:** Add `builder.Services.AddProblemDetails()` if not already present (DocumentsController already uses it).

### 7.2 ~~Update error responses in each controller~~ ✅

- **Files:** All six controllers
- **Action:** Replace bare `NotFound()`, `BadRequest(ModelState)`, `Conflict()`, and `BadRequest("string")` with `Problem(title:, detail:, statusCode:)` or `ValidationProblem(ModelState)`.
- **Source:** Identified in Authorities #6, Bookmarks #7, Cases #12, Members #8, User #7, WorkflowStateHistory #7.
- **Priority within phase:** Start with CasesController (highest traffic/complexity), then Authorities, Members, WorkflowStateHistory, Bookmarks, User.

**Standard patterns:**

```csharp
// Not found
return Problem(title: "Entity not found", detail: $"Authority {key} does not exist.",
    statusCode: StatusCodes.Status404NotFound);

// Concurrency conflict
return Problem(title: "Concurrency conflict",
    detail: "The entity was modified by another user. Refresh and retry.",
    statusCode: StatusCodes.Status409Conflict);

// Validation error
return ValidationProblem(ModelState);

// Checkout conflict (Cases-specific, with detail)
return Problem(title: "Case already checked out",
    detail: $"Case {key} is checked out by {existing.CheckedOutByName}.",
    statusCode: StatusCodes.Status409Conflict);
```

---

## Phase 8 — Role-Based Authorization ✅

**Goal:** Add resource-level authorization beyond simple authentication. Requires defining roles and policies first.

### 8.1 ~~Define authorization policies in `Program.cs`~~ ✅

- **File:** `Program.cs`
- **Action:** Define role-based policies (`Admin`, `CaseManager`, `Viewer`, etc.) using `builder.Services.AddAuthorization(options => ...)`.

### 8.2 ~~AuthoritiesController → Role-based restrictions on mutations~~ ✅

- **File:** `Controllers/AuthoritiesController.cs`
- **Action:** Add `[Authorize(Roles = "Admin,CaseManager")]` on `Post`, `Patch`, `Delete`.
- **Source:** Authorities characterization, Weakness #4.

### 8.3 ~~CasesController → Role-based restriction on Delete~~ ✅

- **File:** `Controllers/CasesController.cs`
- **Action:** Add `[Authorize(Roles = "Admin")]` on `Delete`.
- **Source:** Cases characterization, derived from Weakness #1.

### 8.4 ~~DocumentsController → Role-based restrictions on mutations~~ ✅

- **File:** `Controllers/DocumentsController.cs`
- **Action:** Add `[Authorize(Policy = "CanManageDocuments")]` on `Upload`, `Patch`, `Put`, `Delete`.
- **Source:** Documents characterization, Weakness #5.

---

## Deferred — Architectural Changes (Future Phases)

These are larger refactors that require coordinated client and server changes. They should be planned as separate work items.

### D.1 Replace `Delta<T>` with strongly-typed update DTOs

- **Scope:** All OData controllers using `Delta<T>` (Authorities, Cases, Members, WorkflowStateHistory, Documents)
- **Rationale:** Eliminates the dual-serializer problem (OData formatter on PATCH vs. System.Text.Json on POST/PUT). Removes the entire class of enum serialization risks.
- **Source:** Authorities #2, Cases #4/#6, Members #2/#4, WorkflowStateHistory #4.
- **Effort:** Large — requires new DTO classes, explicit mapping logic, and client-side changes.

### D.2 Remove PUT from MembersController (keep PATCH only)

- **Scope:** `Controllers/MembersController.cs` + client code
- **Rationale:** Three serialization pathways (POST STJ, PUT STJ, PATCH OData) for the same entity is a maintenance hazard.
- **Source:** Members characterization, Weakness #2.

### D.3 Move binary document storage to Azure Blob Storage

- **Scope:** `Controllers/DocumentsController.cs`, new `IBlobStorageService`, EF migration
- **Rationale:** Eliminates database bloat, memory pressure on upload/download, and enables CDN caching.
- **Source:** Documents characterization, Weakness #1/#2/#8.

### D.4 CasesController Delete → database CASCADE or `ExecuteDeleteAsync` chain

- **Scope:** `Controllers/CasesController.cs`, EF model configuration, migration
- **Rationale:** Current delete loads 10 navigation collections (11 SQL queries) before removing. Database-side CASCADE or bulk `ExecuteDeleteAsync` on child tables is far more efficient.
- **Source:** Cases characterization, Weakness #7/#8.

### D.5 Add rate limiting for expensive operations

- **Scope:** `Program.cs`, Documents Upload, PDF generation
- **Rationale:** File uploads and PDF generation are CPU/memory intensive. Unthrottled access allows resource exhaustion.
- **Source:** Documents characterization, Weakness #7.

### D.6 UserController → Named DTO instead of anonymous object

- **Scope:** `Controllers/UserController.cs`, new `CurrentUserDto`
- **Rationale:** Anonymous types cannot be documented in OpenAPI schemas.
- **Source:** User characterization, Weakness #8.

---

## Implementation Order Summary

| Phase | Items | Effort | Risk Reduction | Status |
|-------|-------|--------|----------------|--------|
| **1 — Security** | 1.1, 1.2, 1.3, 1.4 | Small | **Critical** — closes auth bypass | ✅ 4/4 |
| **2 — Concurrency** | 2.1, 2.2, 2.3, 2.4 | Medium | **High** — fixes silent data corruption | ✅ 4/4 |
| **3 — SingleResult** | 3.1, 3.2, 3.3 | Small | Medium — enables proper OData composition | ✅ 3/3 |
| **4 — Performance** | 4.1–4.7 | Small–Medium | Medium — eliminates N+1, extra round trips, and batches OData calls | ✅ 7/7 |
| **5 — Caching** | 5.1, 5.2, 5.3 | Small | Medium — prevents stale data in active workflows | ✅ 3/3 |
| **6 — Logging** | 6.1, 6.2, 6.3 | Small | Low — improves observability | ✅ 3/3 |
| **7 — ProblemDetails** | 7.1, 7.2 | Medium | Low — improves error handling consistency | ✅ 2/2 |
| **8 — RBAC** | 8.1–8.4 | Medium | Medium — adds resource-level authorization | ✅ 4/4 |
| **Deferred** | D.1–D.6 | Large | Variable — architectural improvements | Not started |

**Total immediate items:** 30 (Phases 1–8) — **30 complete, 0 not done, 0 partial**
**Deferred items:** 6 (future work items)

---

## Dependencies

```
Phase 1 (Security) ← no dependencies, start here
Phase 2 (Concurrency) ← 2.2 requires EF migration for RowVersion on WorkflowStateHistory
Phase 3 (SingleResult) ← no dependencies
Phase 4 (Performance) ← no dependencies
Phase 5 (Caching) ← no dependencies
Phase 6 (Logging) ← requires LoggingService interface/implementation updates
Phase 7 (ProblemDetails) ← 7.1 (DI registration) before 7.2 (controller updates)
Phase 8 (RBAC) ← 8.1 (policy definitions) before 8.2–8.4; depends on Phase 1
```

Phases 1–5 can be executed in parallel across developers once Phase 1 is merged (Phase 8 depends on it). Phase 6 and 7 are independent of all other phases.
