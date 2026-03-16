# Implementation Plan — Project Analysis Recommendations

This plan organizes the recommendations from the project analysis into phased, actionable work items. Each phase groups related changes that can be developed and tested together.

---

## Phase 1: Critical Security & Stability (Priority: Immediate)

### 1.1 Replace Console Logging Middleware with Structured Logging

**Problem:** `RequestLoggingMiddleware` uses `Console.WriteLine` to dump full request/response bodies — including SSNs, medical data, and substance abuse history — with zero PII filtering.

**Files:**
- `ECTSystem.Api/Middleware/RequestLoggingMiddleware.cs`

**Steps:**
1. Replace `System.Console.WriteLine` with `ILogger<RequestLoggingMiddleware>` injection
2. Log only method, path, query string, status code, and elapsed time at `Information` level
3. Log full request/response bodies only at `Debug` level and only in Development environment
4. Add PII scrub for known sensitive fields (SSN, medical diagnosis, substance info) before any body logging
5. Use `LoggerMessage.Define` or source-generated logging (consistent with `LoggingService` pattern already in project)

**Validation:** Run API, verify no PII appears in console output; verify structured log entries in Development

---

### 1.2 Add Global Exception Handler

**Problem:** No `UseExceptionHandler()` in the pipeline — unhandled exceptions return raw 500 responses with stack traces.

**Files:**
- `ECTSystem.Api/Program.cs`

**Steps:**
1. Add `app.UseExceptionHandler()` before `UseMiddleware<RequestLoggingMiddleware>()`
2. Configure a `/error` endpoint that returns a generic `ProblemDetails` response (RFC 7807)
3. Log the full exception via `ILogger` inside the handler
4. In Development, optionally include exception details in the response via `UseDeveloperExceptionPage()` guarded by environment check

**Implementation:**
```csharp
// Before existing middleware
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://tools.ietf.org/html/rfc7807",
            title = "An unexpected error occurred",
            status = 500
        });
    });
});
```

**Validation:** Throw a test exception in a controller; verify generic 500 response (no stack trace)

---

### 1.3 Add Security Response Headers

**Problem:** No Content-Security-Policy, X-Frame-Options, or X-Content-Type-Options headers configured.

**Files:**
- `ECTSystem.Api/Program.cs` (add inline middleware or a new `SecurityHeadersMiddleware`)

**Steps:**
1. Add middleware that sets the following headers on every response:
   - `X-Content-Type-Options: nosniff`
   - `X-Frame-Options: DENY`
   - `X-XSS-Protection: 0` (modern approach — rely on CSP instead)
   - `Referrer-Policy: strict-origin-when-cross-origin`
   - `Content-Security-Policy: default-src 'self'` (adjust for Blazor WASM requirements)
2. Place before `UseHttpsRedirection()`

**Validation:** Use browser DevTools Network tab to verify headers appear on API responses

---

### 1.4 Add File Upload MIME Validation

**Problem:** `DocumentFilesController` validates file extension only — MIME type spoofing is possible.

**Files:**
- `ECTSystem.Api/Controllers/DocumentFilesController.cs`

**Steps:**
1. After the existing extension whitelist check, read the first few bytes of the file stream to detect the actual content type (magic bytes / file signature)
2. Create a helper that maps file signatures to expected MIME types (PDF: `%PDF`, JPEG: `FF D8 FF`, PNG: `89 50 4E 47`, etc.)
3. Reject uploads where the detected content type doesn't match the declared extension
4. Add `Content-Disposition: attachment` header on download responses to prevent inline rendering

**Validation:** Upload a `.txt` file renamed to `.pdf`; verify it's rejected

---

## Phase 2: Data Integrity & Correctness (Priority: High)

### 2.1 Fix Non-Deterministic WorkflowStateHistory Ordering

**Problem:** Chained state transitions can create `WorkflowStateHistory` entries with identical `CreatedDate` values (Windows timer resolution ~15.6ms), causing non-deterministic step status display.

**Files:**
- `ECTSystem.Web/Shared/WorkflowSidebar.razor.cs` — anywhere `OrderByDescending(h => h.CreatedDate)` is used
- Any other files that sort history by `CreatedDate` alone

**Steps:**
1. Search all files for `OrderByDescending(h => h.CreatedDate)` patterns on WorkflowStateHistory
2. Add `.ThenByDescending(h => h.Id)` tiebreaker to every occurrence
3. Verify the `Id` property is a server-assigned auto-increment integer (already confirmed)

**Validation:** Create rapid chained transitions in dev; verify sidebar shows correct step statuses

---

### 2.2 Add Optimistic Concurrency to Cases PATCH

**Problem:** `CasesController.Patch()` uses `Delta<T>.Patch()` without RowVersion concurrency checks, allowing last-write-wins data loss. `AuthoritiesController` already implements this correctly.

**Files:**
- `ECTSystem.Api/Controllers/CasesController.cs` — `Patch()` method

**Steps:**
1. Verify `LineOfDutyCase` has a `RowVersion` property (confirmed — already used in the method)
2. Confirm the PATCH method already sets `context.Entry(existing).Property(e => e.RowVersion).OriginalValue` (confirmed — already implemented!)
3. This item is **already resolved** — mark as done

**Status:** ✅ Already implemented (verified in current code at line ~175)

---

### 2.3 Fix CaseId Generation Race Condition

**Problem:** `GenerateCaseIdAsync` uses MAX+1 pattern. Concurrent inserts can race to the same suffix.

**Files:**
- `ECTSystem.Api/Controllers/CasesController.cs` — `GenerateCaseIdAsync()` and `Post()`

**Current mitigation:** Retry loop (3 attempts) with re-generation on `DbUpdateException`. This is a reasonable approach given the unique index on `CaseId` — the retry handles the race.

**Steps (optional hardening):**
1. Consider replacing with a SQL sequence: `CREATE SEQUENCE CaseIdSequence START WITH 1 INCREMENT BY 1`
2. Or use `UPDLOCK` hint in the MAX query to serialize suffix generation
3. **Low urgency** — current retry loop is functional; only matters under very high concurrent case creation

**Status:** ✅ Completed — `GenerateCaseIdAsync()` now uses `UPDLOCK, HOLDLOCK` via raw SQL to serialize concurrent suffix generation, eliminating the race at the database level. The retry loop in `Post()` remains as a safety net.

---

### 2.4 URL-Encode OData Filter Values

**Problem:** `ODataServiceBase.BuildNavigationPropertyUrl()` concatenates filter values without encoding. Values containing `&`, `=`, or `+` break the query string.

**Files:**
- `ECTSystem.Web/Services/ODataServiceBase.cs` — `BuildNavigationPropertyUrl()`

**Steps:**
1. Identify all callers of `BuildNavigationPropertyUrl` to determine if filter values come from user input
2. Apply `Uri.EscapeDataString()` to the filter parameter before appending
3. Alternatively, encode individual filter values at the call sites where user input is passed

**Status:** ✅ Completed — Applied `Uri.EscapeDataString()` to `filter`, `select`, and `orderby` parameter values in `BuildNavigationPropertyUrl()`. Values containing `&`, `=`, `+`, or other special characters are now properly encoded.

**Validation:** Test with a filter value containing `&` — e.g., a member name like "Smith & Wesson"

---

## Phase 3: Performance (Priority: High)

### 3.1 Eliminate Duplicate HTTP Requests for Documents/Tracking

**Problem:** Initial case load includes `$expand=Documents,WorkflowStateHistories` (~85KB), but this data is discarded. When users click the Documents or Tracking tabs, RadzenDataGrid `LoadData` events fetch the same data again.

**Files:**
- `ECTSystem.Web/Pages/EditCase.razor.cs` — `LoadCaseAsync()`, `LoadDocumentsData()`, `LoadTrackingData()`
- `ECTSystem.Web/Pages/EditCase.Documents.razor.cs`
- `ECTSystem.Web/Pages/EditCase.Form348.razor.cs`

**Steps:**
1. In `LoadCaseAsync()`, after fetching the case with `$expand`, populate `_documentsData` and `_trackingData` from the response's navigation properties
2. Modify `LoadDocumentsData()` and `LoadTrackingData()` to use cached data on first load
3. Add a "Refresh" button or flag that triggers a fresh HTTP request for subsequent loads only
4. Consider removing `Documents` and `WorkflowStateHistories` from the initial `$expand` if they're large and rarely viewed (lazy-load instead)

**Validation:** Use browser DevTools Network tab — verify only one request for documents on initial case load + tab click

---

### 3.2 Add Database Indexes

**Problem:** No explicit indexes on frequently queried columns beyond the `CaseId` unique index.

**Files:**
- `ECTSystem.Persistence/Data/Configurations/LineOfDutyCaseConfiguration.cs`
- New migration after adding indexes

**Steps:**
1. Add index on `MemberId` (used in previous-cases lookup and member search)
2. Add index on `CreatedDate` (used in date-range filtering and sorting)
3. Add composite index on `(MemberId, CreatedDate)` for the common "cases for member X sorted by date" query
4. Add index on `WorkflowStateHistory.LineOfDutyCaseId, WorkflowState` (confirmed this may already exist — verify)
5. Generate and apply EF migration

**Status:** ✅ Completed — Added indexes on `CreatedDate` and composite `(MemberId, CreatedDate)` to `LineOfDutyCaseConfiguration`. `MemberId` FK index was already auto-created by EF Core. `WorkflowStateHistory` descending index `(LineOfDutyCaseId, CreatedDate DESC, Id DESC)` also included in migration `AddCaseIndexes`.

**Validation:** Run `EXPLAIN` / execution plans on the most common queries; verify index usage

---

### 3.3 Add HTTP Cache Headers

**Status:** ✅ Completed

**Problem:** No caching on read-only GET endpoints — every request hits the database.

**Files Modified:**
- `ECTSystem.Api/Controllers/CasesController.cs`
- `ECTSystem.Api/Controllers/MembersController.cs`
- `ECTSystem.Api/Controllers/AuthoritiesController.cs`
- `ECTSystem.Api/Controllers/WorkflowStateHistoriesController.cs`
- `ECTSystem.Api/Controllers/DocumentsController.cs`
- `ECTSystem.Api/Controllers/CaseBookmarksController.cs`

**What was implemented:**
1. **ETag + conditional GET** on `CasesController.Get(key)` — generates ETag from `RowVersion` (Base64), checks `If-None-Match`, returns 304 Not Modified when unchanged, sets `Cache-Control: private, max-age=0, must-revalidate`
2. **`[ResponseCache(Duration = 60, Location = Client)]`** on all collection/navigation GET endpoints across Cases, Authorities, WorkflowStateHistories, Documents, CaseBookmarks, and Members `GetLineOfDutyCases()` navigation
3. **`[ResponseCache(NoStore = true, Location = None)]`** on `MembersController` `Get()` and `Get(key)` — PII-sensitive, no caching
4. No server-side caching middleware needed — only HTTP response headers via attributes

---

## Phase 4: Architecture & Maintainability (Priority: Medium)

### 4.1 Extract EditCase Child Components

**Problem:** `EditCase.razor.cs` is a 1000+ line God Component spanning multiple partial files with 30+ injected services. It handles case CRUD, documents, workflow transitions, member search, bookmarks, previous cases, history tracking, validation, and PDF generation.

**Files:**
- `ECTSystem.Web/Pages/EditCase.razor` / `.razor.cs` and all partial files

**Steps:**
1. **Identify boundaries:** Map which fields/methods belong to which concern
2. **Extract `DocumentManager` component:**
   - Move `_documentsData`, `_documentsGrid`, `LoadDocumentsData()`, upload/download logic
   - Parameters: `CaseId`, `Documents` (initial data)
   - Events: `OnDocumentUploaded`, `OnDocumentDeleted`
3. **Extract `WorkflowTransitionPanel` component:**
   - Move workflow action buttons, `TransitionAsync()`, guard display logic
   - Parameters: `Case`, `StateMachine`
   - Events: `OnTransitioned`
4. **Extract `MemberSearchModal` component:**
   - Move member search dialog, `_memberSearchResults`, search/select handlers
   - Events: `OnMemberSelected`
5. **Extract `PreviousCasesGrid` component:**
   - Move `_previousCases`, `LoadPreviousCasesAsync()`
   - Parameters: `MemberId`
6. **Wire up:** Replace inline markup/logic in EditCase with new child components, passing parameters and handling events
7. **Verify:** Each component should be independently testable

**Validation:** All existing functionality works as before; EditCase.razor.cs drops below 300 lines

---

### 4.2 Add API Rate Limiting

**Problem:** No rate limiting configured — API vulnerable to abuse or accidental tight loops from client-side bugs.

**Files:**
- `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs`
- `ECTSystem.Api/Program.cs`

**Steps:**
1. Add `builder.Services.AddRateLimiter()` with a sliding window policy (e.g., 100 requests per minute per authenticated user)
2. Add `app.UseRateLimiter()` after `UseAuthentication()` / `UseAuthorization()`
3. Configure higher limits for read endpoints, lower for write endpoints
4. Return `429 Too Many Requests` with `Retry-After` header

**Status:** ✅ Completed — Added global partitioned rate limiter (sliding window: 100 requests/minute, 4 segments) keyed by authenticated user name or remote IP. Returns `429 Too Many Requests` with `Retry-After: 60` header. Middleware placed after `UseAuthorization()` in the pipeline.

**Validation:** Script rapid requests; verify 429 after hitting limit

---

## Phase 5: Testing (Priority: Medium)

### 5.1 Add Integration Test Harness

**Problem:** No integration tests — API ↔ database round-trips untested.

**Files:**
- New `ECTSystem.Tests/Integration/` folder

**Steps:**
1. Add `Microsoft.AspNetCore.Mvc.Testing` NuGet package
2. Create `WebApplicationFactory<Program>` subclass with in-memory SQLite database
3. Add base class `IntegrationTestBase` with authenticated HTTP client helper
4. Write tests:
   - Create case → verify CaseId generation
   - PATCH case → verify field updates and RowVersion returned
   - Upload document → download and verify content matches
   - Checkout case → verify Checkout rejects second checkout

---

### 5.2 Add Regression Tests for Known Bugs

**Problem:** Repository memory contains multiple verified bugs with no regression tests.

**Files:**
- `ECTSystem.Tests/` — new test files

**Steps:**
1. **Authority data loss test:** Save case with authorities → call SaveCaseAsync → verify authorities are not overwritten by stale DB state
2. **IsLegallySufficient mapping test:** Map a case with "Legally insufficient" → verify `IsLegallySufficient == false` (not corrupted to `true`)
3. **WorkflowStateHistory ordering test:** Create two history entries with same `CreatedDate` but different `Id` → verify correct ordering with tiebreaker
4. **OData Key() vs Filter() test:** Verify `GetCaseAsync` returns valid data (not null) from the Filter/Top pattern

---

### 5.3 Add E2E Workflow Test

**Problem:** No end-to-end test for the Draft → Completed workflow path.

**Files:**
- `ECTSystem.Tests/StateMachines/` — expand existing tests
- Consider `ECTSystem.Tests/E2E/` with Playwright

**Steps:**
1. Add state machine test: fire all triggers from Draft → Completed, verifying each transition
2. Add state machine test: verify guard conditions block illegal transitions
3. (Future) Add Playwright test: fill forms through each step in the browser

---

## Phase 6: Security Hardening (Priority: Lower — Pre-Production)

### 6.1 OData $expand Authorization Filtering

**Problem:** Any authenticated user can `$expand` navigation properties to access data they shouldn't see (e.g., medical assessments, SJA recommendations).

**Files:**
- `ECTSystem.Api/Controllers/CasesController.cs`
- Potentially a new `ODataAuthorizationFilter`

**Steps:**
1. Define which roles can access which navigation properties
2. Implement `IAsyncActionFilter` that inspects `$expand` query option and rejects unauthorized expansions
3. Apply to controllers via attribute or convention

---

### 6.2 Row-Level Security

**Problem:** All authenticated users can query all cases. No case-ownership or role-based filtering.

**Steps:**
1. Add claims-based role system (e.g., MedTech, Commander, SJA, WingCC, Admin)
2. Add query filter in DbContext: `modelBuilder.Entity<LineOfDutyCase>().HasQueryFilter(...)` scoped by user's unit/role
3. Add `[Authorize(Policy = "...")]` attributes on sensitive endpoints

---

### 6.3 Strengthen Password Policy for Production

**Files:**
- `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` — `AddIdentity()`

**Steps:**
1. Move current relaxed policy behind `IsDevelopment()` check
2. Add production-strength policy: min 12 chars, require digit + uppercase + special char
3. Consider CAC/PIV integration for military deployment (future)

---

## Phase Summary

| Phase | Items | Scope | Dependencies |
|-------|-------|-------|-------------|
| **Phase 1** | 1.1–1.4 | API security & stability | None |
| **Phase 2** | 2.1–2.4 | Data integrity fixes | None |
| **Phase 3** | 3.1–3.3 | Performance optimization | None |
| **Phase 4** | 4.1–4.2 | Architecture improvements | Phase 1 |
| **Phase 5** | 5.1–5.3 | Test coverage | Phase 2 (for regression tests) |
| **Phase 6** | 6.1–6.3 | Pre-production security | Phase 1 |

## Items Already Resolved

- ✅ **Optimistic concurrency on Cases PATCH** — RowVersion check already implemented
- ✅ **CaseId generation race condition** — retry loop with unique index handles this adequately
- ✅ **SQL injection** — EF Core OData parameterization handles this

## Existing Plans in `docs/` to Incorporate

The following existing documents in `docs/` align with recommendations and should be consulted:
- `implement-validation-summary.md` — Form validation UI improvements
- `case-detail-performance-plan.md` — Performance optimization strategy
- `split-data-service-plan.md` — Service layer decomposition
- `role-based-security-in-state-machine-guards.md` — Security in workflow transitions
- `future-optimizations.md` — Additional optimization ideas
