# Document Upload Optimization Plan

Identified from API/EF log trace of a document upload + grid refresh flow.

## Status Legend
- ⬜ Not started
- 🟡 In progress
- ✅ <span style="color:green">Completed</span>

---

## Fix 1 — CORS Preflight Caching ⬜

**File:** `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` (`AddCorsPolicy`)

**Problem:** Policy has no `SetPreflightMaxAge`, so browser sends an `OPTIONS` preflight before every credentialed request.

**Change:** Add `.SetPreflightMaxAge(TimeSpan.FromMinutes(10))` to the policy builder.

---

## Fix 2 — Post-Upload Optimistic Update ⬜

**File:** `ECTSystem.Web/Pages/EditCase.Documents.razor.cs` (`OnUploadComplete`)

**Problem:** After a successful upload, the parsed response is discarded and `_documentsGrid?.Reload()` triggers a full `GET /odata/Documents?$count=true` round-trip.

**Change:**
- Extract response parsing into a `ParseUploadResponse` helper.
- On success, prepend uploaded documents to `_documentsData` and increment `_documentsCount`.
- Fall back to `Reload()` only if parsing fails.

---

## Fix 3 — Fold Case Existence Check Into Transaction ⬜

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (`Upload`)

**Problem:** `AnyAsync` runs as a standalone DB round-trip outside the execution strategy, then the strategy opens a new connection for the transaction.

**Change:**
- Remove the standalone pre-check.
- Move the same `AnyAsync` to the first statement inside `strategy.ExecuteAsync` (before the transaction is opened, so no orphaned blobs).
- Throw a sentinel `KeyNotFoundException` and translate it to a 404 in the outer catch.

---

## Fix 4 — Composite Index for Document List Queries ⬜

**File:** New EF Core migration in `ECTSystem.Persistence/Migrations/`

**Problem:** Both the paged SELECT and the `COUNT_BIG(*)` query filter by `LineOfDutyCaseId` and order by `UploadDate desc, Id desc`. Without a composite index, both are scans.

**Change:** Add migration creating `IX_Documents_LineOfDutyCaseId_UploadDate_Id` on `(LineOfDutyCaseId ASC, UploadDate DESC, Id DESC)`.

---

## Execution Order

| # | Fix | Effort |
|---|-----|--------|
| 1 | CORS max-age | 1 line |
| 2 | Optimistic update | ~30 lines |
| 3 | Composite index migration | migration |
| 4 | Case check inside transaction | ~10 lines |
