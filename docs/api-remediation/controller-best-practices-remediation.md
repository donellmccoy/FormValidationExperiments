# Controller Best Practices Remediation Plan

> Generated: 2026-04-16  
> Updated: 2026-04-16  
> Scope: `ECTSystem.Api/Controllers/`  
> Status: **Complete** — all phases implemented, solution builds cleanly

---

## Phase 1 — Critical Security & Data Integrity

### 1.1 Replace Raw Entity Parameters with DTOs ✅

**Status:** Complete

**Files:**
- `CaseDialogueCommentsController.cs` — `Post([FromBody] CaseDialogueComment)`
- `DocumentsController.cs` — `Put([FromBody] LineOfDutyDocument)`

**Problem:** Accepting raw entity types exposes every property to the client, enabling mass assignment / over-posting. Clients can set `Id`, `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, and other internal fields.

**Fix:**
1. Create `CreateCaseDialogueCommentDto` in `ECTSystem.Shared/ViewModels/` with only client-writable fields.
2. Create `UpdateDocumentDto` in `ECTSystem.Shared/ViewModels/` with only client-writable fields.
3. Create corresponding mapper methods in `ECTSystem.Shared/Mapping/`.
4. Update controller actions to accept the DTOs and use the mappers.

**Implemented:** Created `CreateCaseDialogueCommentDto`, `UpdateDocumentDto`, `CaseDialogueCommentDtoMapper`, and `DocumentDtoMapper`. Updated `CaseDialogueCommentsController.Post` and `DocumentsController.Put` to accept DTOs. Updated `DocumentsControllerTests` Put tests to use `UpdateDocumentDto`.

**Risk:** Low — additive change; existing clients need to stop sending extra fields (which they shouldn't be sending).

---

### 1.2 Add Ownership Check on Comment Delete ✅

**Status:** Complete

**File:** `CaseDialogueCommentsController.cs` — `Delete(int key)`

**Problem:** Any authenticated user can delete any comment. No ownership or role check.

**Fix:**
1. After loading the comment, verify `comment.CreatedBy == GetAuthenticatedUserId()` or the user is in an Admin/CaseManager role.
2. Return `403 Forbidden` if the check fails.

**Implemented:** Added ownership check — verifies `comment.CreatedBy == GetAuthenticatedUserId()` or `User.IsInRole("Admin") || User.IsInRole("CaseManager")`. Returns 403 Forbidden ProblemDetails if check fails.

**Risk:** Low — purely restrictive change.

---

### 1.3 Fix Broken Concurrency Check in DocumentsController.Patch ✅

**Status:** Complete

**File:** `DocumentsController.cs` — `Patch(int key, Delta<LineOfDutyDocument> delta)`

**Problem:** The original RowVersion is captured **after** `delta.Patch(existing)`, so the concurrency check is a no-op because `existing.RowVersion` has already been overwritten by the delta.

**Fix:**
```csharp
// BEFORE delta.Patch()
var originalRowVersion = existing.RowVersion;
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;
```

**Implemented:** Reordered to capture `originalRowVersion` before `delta.Patch()` and set it as `OriginalValue` afterward.

**Risk:** Low — single-line reorder.

---

### 1.4 Add Concurrency Check to CaseDialogueCommentsController.Patch ✅

**Status:** Complete

**File:** `CaseDialogueCommentsController.cs` — `Patch(int key, Delta<CaseDialogueComment> delta)`

**Problem:** No optimistic concurrency control at all, unlike every other controller.

**Fix:**
1. Save `originalRowVersion` before `delta.Patch()`.
2. Set `context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion`.
3. Catch `DbUpdateConcurrencyException` and return `409 Conflict`.

**Implemented:** Added full optimistic concurrency pattern — captures `originalRowVersion` before `delta.Patch()`, sets `OriginalValue`, catches `DbUpdateConcurrencyException` and returns 409 Conflict ProblemDetails.

**Risk:** Low — aligns with existing pattern.

---

## Phase 2 — Authorization & Access Control

### 2.1 Remove `[EnableQuery]` from Non-GET Endpoints ✅

**Status:** Complete

**Files:** All 7 OData controllers.

**Problem:** `[EnableQuery]` on `Post`, `Put`, `Patch`, `Delete` lets clients compose arbitrary `$filter`/`$expand`/`$select` on mutation responses, adding unexpected DB load and violating OData conventions.

**Fix:** Remove `[EnableQuery]` from every non-GET action. Mutation endpoints should return the created/updated entity directly without query composition.

**Affected actions (by controller):**
| Controller | Actions to fix |
|---|---|
| CasesController | Post, Patch |
| MembersController | Post, Put, Patch |
| AuthoritiesController | Post, Patch |
| BookmarksController | Post |
| DocumentsController | Put, Patch |
| WorkflowStateHistoryController | Post |
| CaseDialogueCommentsController | Post |

> **Note:** `CasesController.ByCurrentState` is an `[HttpPost]` that returns `IQueryable` for OData composition — `[EnableQuery]` is intentional there since it acts as a parameterized query, not a mutation.

**Implemented:** Removed `[EnableQuery]` from all non-GET mutation actions across all 7 controllers. `ByCurrentState` retained as documented.

**Risk:** Medium — clients relying on `$expand` in mutation responses will need to issue a follow-up GET. Verify no client code depends on this.

---

### 2.2 Add Role-Based Authorization to Write Operations ✅

**Status:** Complete

**Files:**
- `MembersController.cs` — Post, Put, Patch, Delete have no role restriction
- `DocumentsController.cs` — Upload, Put, Patch, Delete have no role restriction
- `WorkflowStateHistoryController.cs` — Post, Patch have no role restriction
- `CaseDialogueCommentsController.cs` — Post, Patch, Delete have no role restriction

**Problem:** Any authenticated user can create, update, or delete these resources. Members and workflow state history are particularly sensitive.

**Fix:**
1. Add `[Authorize(Roles = "Admin,CaseManager")]` to write operations on `MembersController`.
2. Add `[Authorize(Roles = "Admin,CaseManager")]` to `DocumentsController` write operations (or scope to case ownership).
3. Add `[Authorize(Roles = "Admin,CaseManager")]` to `WorkflowStateHistoryController` write operations.
4. Determine appropriate roles for `CaseDialogueCommentsController` (comments may be user-scoped).

**Implemented:** Added `[Authorize(Roles = "Admin,CaseManager")]` to write operations on `MembersController`, `DocumentsController`, `WorkflowStateHistoryController`, and `CaseDialogueCommentsController`.

**Risk:** Medium — must coordinate with front-end role assignments. Requires testing all user roles.

---

### 2.3 Fix CasesController.Delete Concurrency Check ✅

**Status:** Complete

**File:** `CasesController.cs` — `Delete(int key)`

**Problem:** Uses the server-loaded RowVersion as the original value, making the concurrency check a no-op (original == current immediately after loading).

**Fix:**
1. Require `If-Match` header (same as `Patch`).
2. Parse the ETag to get the client-supplied RowVersion.
3. Set `context.Entry(lodCase).Property(e => e.RowVersion).OriginalValue = clientRowVersion`.

**Implemented:** Added `If-Match` header requirement to Delete. Parses ETag, sets `OriginalValue` from client-supplied RowVersion. Returns 412 Precondition Failed if header is missing, 409 Conflict on concurrency mismatch.

**Risk:** Medium — existing Delete callers must start sending the `If-Match` header.

---

### 2.4 Add Ownership Check to Case Checkin ✅

**Status:** Complete

**File:** `CasesController.cs` — `Checkin` action

**Problem:** Any authenticated user can check in a case that was checked out by a different user.

**Fix:**
1. After loading the case, verify `existing.CheckedOutBy == GetAuthenticatedUserId()` or user is Admin.
2. Return `403 Forbidden` if the check fails.

**Implemented:** Added ownership check — verifies `existing.CheckedOutBy == GetAuthenticatedUserId()` or `User.IsInRole("Admin")`. Returns 403 Forbidden ProblemDetails if check fails.

**Risk:** Low — restrictive change. May need Admin override for abandoned checkouts.

---

## Phase 3 — Robustness & Testability

### 3.1 Inject `TimeProvider` Instead of `DateTime.UtcNow` ✅

**Status:** Complete

**Files:**
- `CasesController.cs` — `InitiationDate`, `WorkflowStateHistory.EntryDate`, `CheckedOutDate`
- Any other controller using `DateTime.UtcNow`

**Problem:** Hard-coded `DateTime.UtcNow` makes time-dependent logic untestable.

**Fix:**
1. Register `TimeProvider.System` in DI (`Program.cs`).
2. Add `TimeProvider` parameter to `ODataControllerBase` constructor (or inject per-controller).
3. Replace `DateTime.UtcNow` with `timeProvider.GetUtcNow().UtcDateTime`.

**Implemented:** Registered `TimeProvider.System` as singleton in `Program.cs`. Added `TimeProvider` to `ODataControllerBase` constructor. All 6 OData controllers accept and forward `TimeProvider`. Replaced all `DateTime.UtcNow` usages with `TimeProvider.GetUtcNow().UtcDateTime`. Updated all 6 test files to pass `TimeProvider.System`.

**Risk:** Low — additive DI change.

---

### 3.2 Narrow Retry Loop Exception Filter ✅

**Status:** Complete

**File:** `CasesController.cs` — `Post` action, CaseId generation retry loop

**Problem:** Catches all `DbUpdateException`, not just unique constraint violations. Foreign key errors, check constraint failures, etc. are silently retried.

**Fix:**
```csharp
catch (DbUpdateException ex) when (attempt < maxRetries && IsUniqueConstraintViolation(ex))
```
Add a helper that inspects `ex.InnerException` for the SQL Server error number (2601/2627 for unique violations).

**Implemented:** Added `IsUniqueConstraintViolation(DbUpdateException)` helper method that checks for `SqlException` inner exception with error numbers 2601 or 2627. Retry catch clause now uses `when` filter to only retry on unique constraint violations.

**Risk:** Low — more precise error handling.

---

### 3.3 Use `ContentDispositionHeaderValue` for File Downloads ✅

**Status:** Complete

**File:** `DocumentsController.cs` — `GetValue` action

**Problem:** Filenames with quotes, semicolons, or non-ASCII characters break the manually formatted `Content-Disposition` header.

**Fix:**
```csharp
var cd = new ContentDispositionHeaderValue("attachment");
cd.SetHttpFileName(doc.FileName);
Response.Headers.ContentDisposition = cd.ToString();
```

**Implemented:** Replaced manual string formatting with `ContentDispositionHeaderValue` and `SetHttpFileName()` for safe header encoding.

**Risk:** Low — single-method change.

---

### 3.4 Replace `UnauthorizedAccessException` with HTTP Response ✅

**Status:** Complete

**Files:**
- `ODataControllerBase.cs` — `GetAuthenticatedUserId()`
- `UserController.cs` — `GetAuthenticatedUserId()` (duplicated logic)

**Problem:** Throwing `UnauthorizedAccessException` results in an unhandled 500 if `[Authorize]` middleware is ever misconfigured.

**Fix (Option A):** Add a global exception filter that maps `UnauthorizedAccessException` → 401.  
**Fix (Option B):** Change the method to return `string?` and let callers return `Unauthorized()`.

**Implemented:** Option A — Created `UnauthorizedAccessMiddleware` that catches `UnauthorizedAccessException` and returns 401 ProblemDetails. Registered in `Program.cs` pipeline. Additionally, `ClaimsPrincipalExtensions.GetRequiredUserId()` extension method extracted for shared use (see 4.4).

**Risk:** Low — defensive improvement.

---

## Phase 4 — Code Quality & Consistency

### 4.1 Add `[ResponseCache(NoStore = true)]` Consistently ✅

**Status:** Complete

**Files:** All OData controllers with GET endpoints.

**Problem:** Inconsistent caching directives across controllers. Some GET endpoints have `[ResponseCache(NoStore = true)]`, others don't.

**Fix:** Apply `[ResponseCache(NoStore = true)]` to all GET endpoints, or define a consistent caching strategy.

**Implemented:** Applied `[ResponseCache(NoStore = true)]` to all GET endpoints across `BookmarksController`, `DocumentsController`, and `CaseDialogueCommentsController`. All controllers now have consistent cache directives.

**Risk:** Low.

---

### 4.2 Remove Detailed Exception Messages from Logs ✅

**Status:** Complete

**File:** `MembersController.cs` — `Post` action

**Problem:** Logs `e.Exception.Message` from ModelState errors, which could contain sensitive data in production.

**Fix:** Log only the error key and a sanitized message, not the raw exception message.

**Implemented:** Replaced `e.Exception.Message` logging with sanitized `e.ErrorMessage` (the display-safe validation message). PII-bearing exception details are no longer logged.

**Risk:** Low.

---

### 4.3 Add XML Documentation to CaseDialogueCommentsController ✅

**Status:** Complete

**File:** `CaseDialogueCommentsController.cs`

**Problem:** Most methods have no XML doc comments, unlike every other controller.

**Fix:** Add `<summary>`, `<param>`, `<returns>`, and `<response>` XML doc tags matching the style of the other controllers.

**Implemented:** Added full XML documentation (`<summary>`, `<param>`, `<returns>`, `<response>`) to all 4 public action methods (Get, Post, Patch, Delete).

**Risk:** None.

---

### 4.4 Reduce UserController Duplication ✅

**Status:** Complete

**File:** `UserController.cs`

**Problem:** Duplicates `GetAuthenticatedUserId()` logic from `ODataControllerBase`. Since `UserController` inherits `ControllerBase` (not `ODataControllerBase`), the helper is copy-pasted.

**Fix:** Extract `GetAuthenticatedUserId()` into a shared extension method on `ClaimsPrincipal` or a service, then use it from both base classes.

**Implemented:** Created `ClaimsPrincipalExtensions.GetRequiredUserId()` extension method in `ECTSystem.Api/Extensions/`. `ODataControllerBase.GetAuthenticatedUserId()` now delegates to `User.GetRequiredUserId()`. `UserController` uses the same extension method, eliminating duplication.

**Risk:** Low.

---

## Phase 5 — Information Disclosure

### 5.1 Restrict User Lookup Endpoint ✅

**Status:** Complete

**File:** `UserController.cs` — `LookupUsers`

**Problem:** Any authenticated user can enumerate usernames/emails for arbitrary user IDs.

**Fix:** Add `[Authorize(Roles = "Admin,CaseManager")]` or restrict to users who share a case with the looked-up IDs.

**Implemented:** Added `[Authorize(Roles = "Admin,CaseManager")]` to the `LookupUsers` endpoint.

**Risk:** Medium — front-end may depend on this for display names. May need a limited "display name only" endpoint for regular users.

---

## Implementation Order

| Priority | Items | Status |
|---|---|---|
| **P0 — Do First** | 1.1, 1.2, 1.3, 1.4 | ✅ Complete |
| **P1 — Soon** | 2.1, 2.2, 2.3, 2.4 | ✅ Complete |
| **P2 — Next Sprint** | 3.1, 3.2, 3.3, 3.4 | ✅ Complete |
| **P3 — Backlog** | 4.1, 4.2, 4.3, 4.4, 5.1 | ✅ Complete |

---

## Testing Checklist

- [x] Verify all mutation endpoints reject over-posted fields
- [x] Verify comment delete returns 403 for non-owner/non-admin
- [x] Verify concurrent updates to documents return 409
- [x] Verify concurrent updates to comments return 409
- [ ] Verify `$expand` on POST/PATCH returns 400 or is ignored (after EnableQuery removal)
- [ ] Verify role-restricted endpoints return 403 for unauthorized roles
- [ ] Verify Delete with stale RowVersion returns 409
- [ ] Verify Checkin by non-checkout-owner returns 403
- [x] Verify file downloads with special characters in filenames work correctly
- [x] Verify user lookup restricted to authorized roles
- [x] Solution builds cleanly with 0 errors (verified 2026-04-16)
