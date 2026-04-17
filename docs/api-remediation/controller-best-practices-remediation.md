# Controller Best Practices Remediation Plan

> Generated: 2026-04-16  
> Scope: `ECTSystem.Api/Controllers/`  
> Status: **Planned**

---

## Phase 1 — Critical Security & Data Integrity

### 1.1 Replace Raw Entity Parameters with DTOs

**Files:**
- `CaseDialogueCommentsController.cs` — `Post([FromBody] CaseDialogueComment)`
- `DocumentsController.cs` — `Put([FromBody] LineOfDutyDocument)`

**Problem:** Accepting raw entity types exposes every property to the client, enabling mass assignment / over-posting. Clients can set `Id`, `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate`, and other internal fields.

**Fix:**
1. Create `CreateCaseDialogueCommentDto` in `ECTSystem.Shared/ViewModels/` with only client-writable fields.
2. Create `UpdateDocumentDto` in `ECTSystem.Shared/ViewModels/` with only client-writable fields.
3. Create corresponding mapper methods in `ECTSystem.Shared/Mapping/`.
4. Update controller actions to accept the DTOs and use the mappers.

**Risk:** Low — additive change; existing clients need to stop sending extra fields (which they shouldn't be sending).

---

### 1.2 Add Ownership Check on Comment Delete

**File:** `CaseDialogueCommentsController.cs` — `Delete(int key)`

**Problem:** Any authenticated user can delete any comment. No ownership or role check.

**Fix:**
1. After loading the comment, verify `comment.CreatedBy == GetAuthenticatedUserId()` or the user is in an Admin/CaseManager role.
2. Return `403 Forbidden` if the check fails.

**Risk:** Low — purely restrictive change.

---

### 1.3 Fix Broken Concurrency Check in DocumentsController.Patch

**File:** `DocumentsController.cs` — `Patch(int key, Delta<LineOfDutyDocument> delta)`

**Problem:** The original RowVersion is captured **after** `delta.Patch(existing)`, so the concurrency check is a no-op because `existing.RowVersion` has already been overwritten by the delta.

**Fix:**
```csharp
// BEFORE delta.Patch()
var originalRowVersion = existing.RowVersion;
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;
```

**Risk:** Low — single-line reorder.

---

### 1.4 Add Concurrency Check to CaseDialogueCommentsController.Patch

**File:** `CaseDialogueCommentsController.cs` — `Patch(int key, Delta<CaseDialogueComment> delta)`

**Problem:** No optimistic concurrency control at all, unlike every other controller.

**Fix:**
1. Save `originalRowVersion` before `delta.Patch()`.
2. Set `context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion`.
3. Catch `DbUpdateConcurrencyException` and return `409 Conflict`.

**Risk:** Low — aligns with existing pattern.

---

## Phase 2 — Authorization & Access Control

### 2.1 Remove `[EnableQuery]` from Non-GET Endpoints

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

**Risk:** Medium — clients relying on `$expand` in mutation responses will need to issue a follow-up GET. Verify no client code depends on this.

---

### 2.2 Add Role-Based Authorization to Write Operations

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

**Risk:** Medium — must coordinate with front-end role assignments. Requires testing all user roles.

---

### 2.3 Fix CasesController.Delete Concurrency Check

**File:** `CasesController.cs` — `Delete(int key)`

**Problem:** Uses the server-loaded RowVersion as the original value, making the concurrency check a no-op (original == current immediately after loading).

**Fix:**
1. Require `If-Match` header (same as `Patch`).
2. Parse the ETag to get the client-supplied RowVersion.
3. Set `context.Entry(lodCase).Property(e => e.RowVersion).OriginalValue = clientRowVersion`.

**Risk:** Medium — existing Delete callers must start sending the `If-Match` header.

---

### 2.4 Add Ownership Check to Case Checkin

**File:** `CasesController.cs` — `Checkin` action

**Problem:** Any authenticated user can check in a case that was checked out by a different user.

**Fix:**
1. After loading the case, verify `existing.CheckedOutBy == GetAuthenticatedUserId()` or user is Admin.
2. Return `403 Forbidden` if the check fails.

**Risk:** Low — restrictive change. May need Admin override for abandoned checkouts.

---

## Phase 3 — Robustness & Testability

### 3.1 Inject `TimeProvider` Instead of `DateTime.UtcNow`

**Files:**
- `CasesController.cs` — `InitiationDate`, `WorkflowStateHistory.EntryDate`, `CheckedOutDate`
- Any other controller using `DateTime.UtcNow`

**Problem:** Hard-coded `DateTime.UtcNow` makes time-dependent logic untestable.

**Fix:**
1. Register `TimeProvider.System` in DI (`Program.cs`).
2. Add `TimeProvider` parameter to `ODataControllerBase` constructor (or inject per-controller).
3. Replace `DateTime.UtcNow` with `timeProvider.GetUtcNow().UtcDateTime`.

**Risk:** Low — additive DI change.

---

### 3.2 Narrow Retry Loop Exception Filter

**File:** `CasesController.cs` — `Post` action, CaseId generation retry loop

**Problem:** Catches all `DbUpdateException`, not just unique constraint violations. Foreign key errors, check constraint failures, etc. are silently retried.

**Fix:**
```csharp
catch (DbUpdateException ex) when (attempt < maxRetries && IsUniqueConstraintViolation(ex))
```
Add a helper that inspects `ex.InnerException` for the SQL Server error number (2601/2627 for unique violations).

**Risk:** Low — more precise error handling.

---

### 3.3 Use `ContentDispositionHeaderValue` for File Downloads

**File:** `DocumentsController.cs` — `GetValue` action

**Problem:** Filenames with quotes, semicolons, or non-ASCII characters break the manually formatted `Content-Disposition` header.

**Fix:**
```csharp
var cd = new ContentDispositionHeaderValue("attachment");
cd.SetHttpFileName(doc.FileName);
Response.Headers.ContentDisposition = cd.ToString();
```

**Risk:** Low — single-method change.

---

### 3.4 Replace `UnauthorizedAccessException` with HTTP Response

**Files:**
- `ODataControllerBase.cs` — `GetAuthenticatedUserId()`
- `UserController.cs` — `GetAuthenticatedUserId()` (duplicated logic)

**Problem:** Throwing `UnauthorizedAccessException` results in an unhandled 500 if `[Authorize]` middleware is ever misconfigured.

**Fix (Option A):** Add a global exception filter that maps `UnauthorizedAccessException` → 401.  
**Fix (Option B):** Change the method to return `string?` and let callers return `Unauthorized()`.

**Risk:** Low — defensive improvement.

---

## Phase 4 — Code Quality & Consistency

### 4.1 Add `[ResponseCache(NoStore = true)]` Consistently

**Files:** All OData controllers with GET endpoints.

**Problem:** Inconsistent caching directives across controllers. Some GET endpoints have `[ResponseCache(NoStore = true)]`, others don't.

**Fix:** Apply `[ResponseCache(NoStore = true)]` to all GET endpoints, or define a consistent caching strategy.

**Risk:** Low.

---

### 4.2 Remove Detailed Exception Messages from Logs

**File:** `MembersController.cs` — `Post` action

**Problem:** Logs `e.Exception.Message` from ModelState errors, which could contain sensitive data in production.

**Fix:** Log only the error key and a sanitized message, not the raw exception message.

**Risk:** Low.

---

### 4.3 Add XML Documentation to CaseDialogueCommentsController

**File:** `CaseDialogueCommentsController.cs`

**Problem:** Most methods have no XML doc comments, unlike every other controller.

**Fix:** Add `<summary>`, `<param>`, `<returns>`, and `<response>` XML doc tags matching the style of the other controllers.

**Risk:** None.

---

### 4.4 Reduce UserController Duplication

**File:** `UserController.cs`

**Problem:** Duplicates `GetAuthenticatedUserId()` logic from `ODataControllerBase`. Since `UserController` inherits `ControllerBase` (not `ODataControllerBase`), the helper is copy-pasted.

**Fix:** Extract `GetAuthenticatedUserId()` into a shared extension method on `ClaimsPrincipal` or a service, then use it from both base classes.

**Risk:** Low.

---

## Phase 5 — Information Disclosure

### 5.1 Restrict User Lookup Endpoint

**File:** `UserController.cs` — `LookupUsers`

**Problem:** Any authenticated user can enumerate usernames/emails for arbitrary user IDs.

**Fix:** Add `[Authorize(Roles = "Admin,CaseManager")]` or restrict to users who share a case with the looked-up IDs.

**Risk:** Medium — front-end may depend on this for display names. May need a limited "display name only" endpoint for regular users.

---

## Implementation Order

| Priority | Items | Estimated Scope |
|---|---|---|
| **P0 — Do First** | 1.1, 1.2, 1.3, 1.4 | 4 files, ~6 new files (DTOs + mappers) |
| **P1 — Soon** | 2.1, 2.2, 2.3, 2.4 | 8 files |
| **P2 — Next Sprint** | 3.1, 3.2, 3.3, 3.4 | 4 files |
| **P3 — Backlog** | 4.1, 4.2, 4.3, 4.4, 5.1 | 5 files |

---

## Testing Checklist

- [ ] Verify all mutation endpoints reject over-posted fields
- [ ] Verify comment delete returns 403 for non-owner/non-admin
- [ ] Verify concurrent updates to documents return 409
- [ ] Verify concurrent updates to comments return 409
- [ ] Verify `$expand` on POST/PATCH returns 400 or is ignored (after EnableQuery removal)
- [ ] Verify role-restricted endpoints return 403 for unauthorized roles
- [ ] Verify Delete with stale RowVersion returns 409
- [ ] Verify Checkin by non-checkout-owner returns 403
- [ ] Verify file downloads with special characters in filenames work correctly
- [ ] Verify user lookup restricted to authorized roles
