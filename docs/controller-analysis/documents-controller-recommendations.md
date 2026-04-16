# Implementation Plan: DocumentsController Recommendations

## Phase 1 — Quick Wins (low risk, no client changes)

### Step 1: Fix `Get(key)` to return `IQueryable`

- **File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (lines 82–97)
- **What:** Replace eager `FirstOrDefaultAsync` materialization with an `IQueryable` return via `SingleResult.Create()`, matching the OData convention for single-entity GET
- **Why:** Enables full OData composition (`$select`, `$expand`) on single-entity queries; currently the `[EnableQuery]` attribute is wasted because the entity is already materialized
- **Pattern:** Use `CreateContextAsync` (registers context for disposal during serialization) + `SingleResult.Create(query)`:
  ```csharp
  var context = await CreateContextAsync(ct);
  var query = context.Documents.AsNoTracking().Where(d => d.Id == key);
  return Ok(SingleResult.Create(query));
  ```
- **Trade-off:** Logging `RetrievingDocument` cannot include `LineOfDutyCaseId` without materializing. Log only the key, or move to `QueryingDocuments()`. The `DocumentNotFound` 404 is now handled by OData returning an empty result.
- **Risk:** Low — read-only change, no client-side impact

### Step 2: Simplify `Delete` with `ExecuteDeleteAsync` + remove dead code

- **File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (lines 219–258)
- **What:** Replace the 2-round-trip `FindAsync` + `Remove` + `SaveChangesAsync` pattern with a single-SQL `ExecuteDeleteAsync`. Remove all commented-out dead code.
- **New implementation:**
  ```csharp
  var deleted = await context.Documents
      .Where(d => d.Id == key)
      .ExecuteDeleteAsync(ct);
  if (deleted == 0) return NotFound();
  return NoContent();
  ```
- **Why:** Single SQL `DELETE WHERE Id = @key` — no entity load, no change tracker overhead, no concurrency exception handling needed
- **Risk:** Low — `ExecuteDeleteAsync` bypasses `RowVersion` concurrency checks, but deletes don't need optimistic concurrency (if it's gone, it's gone). The `catch (DbUpdateConcurrencyException)` block becomes unnecessary.
- **Note:** `ExecuteDeleteAsync` is not currently used in any sibling controller — all use `FindAsync` + `Remove`. This would be the first usage in the codebase.
- **Client impact:** None — the client already calls `DELETE /odata/Documents({id})` and expects `204 NoContent`

---

## Phase 2 — Add PATCH for metadata updates (moderate effort)

### Step 3: Add `Patch` method using `Delta<T>`

- **File:** `ECTSystem.Api/Controllers/DocumentsController.cs` — insert after the `Get(key)` method
- **What:** Add a PATCH endpoint following the established pattern from `CasesController`, `MembersController`, and `AuthoritiesController`
- **Pattern to follow** (from `AuthoritiesController`):
  ```csharp
  public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyDocument> delta, CancellationToken ct)
  {
      if (delta is null || !ModelState.IsValid) return BadRequest(ModelState);
      await using var context = await ContextFactory.CreateDbContextAsync(ct);
      var existing = await context.Documents.FindAsync([key], ct);
      if (existing is null) return NotFound();
      delta.Patch(existing);
      context.Entry(existing).Property(e => e.RowVersion).OriginalValue = existing.RowVersion;
      try { await context.SaveChangesAsync(ct); }
      catch (DbUpdateConcurrencyException) { return Conflict(); }
      return Updated(existing);
  }
  ```
- **Why:** Allows updating metadata fields (`DocumentType`, `Description`) without re-uploading the file. `Delta<T>` applies only changed properties. `RowVersion` concurrency is already on `AuditableEntity`.
- **Logging:** Add `PatchingDocument(int documentId)` and `DocumentPatched(int documentId)` to `LoggingService` (EventIds 312–313)
- **Client impact:** None until the client is updated to use PATCH. No breaking change.

### Step 4: Add `Put` method for full metadata replace (optional)

- **File:** `ECTSystem.Api/Controllers/DocumentsController.cs` — insert after PATCH
- **What:** Add a PUT endpoint following the pattern from `MembersController`
- **Pattern:**
  ```csharp
  public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] LineOfDutyDocument entity, CancellationToken ct)
  {
      if (!ModelState.IsValid) return BadRequest(ModelState);
      if (key != entity.Id) return BadRequest("Key mismatch");
      await using var context = await ContextFactory.CreateDbContextAsync(ct);
      var existing = await context.Documents.FindAsync([key], ct);
      if (existing is null) return NotFound();
      context.Entry(existing).Property(e => e.RowVersion).OriginalValue = entity.RowVersion;
      context.Entry(existing).CurrentValues.SetValues(entity);
      try { await context.SaveChangesAsync(ct); }
      catch (DbUpdateConcurrencyException) { return Conflict(); }
      return Updated(existing);
  }
  ```
- **Consideration:** PUT replaces all properties including `Content` (byte[]). For documents, PATCH is more practical since clients rarely want to replace binary content through a JSON PUT. Could exclude `Content` from `SetValues`. **Suggest PATCH only initially; add PUT later if needed.**
- **Risk:** Medium — need to decide whether `Content` should be replaceable via PUT or excluded

---

## Phase 3 — Base class migration (most invasive, deferred)

### Step 5: Switch from `ODataController` to `ControllerBase` with attribute routing

- **Files:** `ECTSystem.Api/Controllers/ODataControllerBase.cs`, **all controllers inheriting it**
- **What:** Change `ODataControllerBase : ODataController` to `ODataControllerBase : ControllerBase`, add `[Route("odata/[controller]")]` and `[ApiController]` to each controller
- **Why:** The recommendation argues OData convention routing (magic method naming) is brittle; explicit attribute routing is more discoverable
- **Risk:** **High** — This affects **all** controllers in the API. OData convention routing resolves action methods by name (`Get`, `Post`, `Patch`, `Delete`). Switching to `ControllerBase` breaks convention routing, requiring explicit `[HttpGet]`, `[HttpPatch]`, etc. on every action. Also loses `Updated()` and `Created()` OData helpers (would need `return Ok(entity)` instead).
- **Recommendation:** **Defer this step.** The project already uses OData convention routing consistently across all controllers with `Delta<T>`, `[EnableQuery]`, bound actions, etc. The migration cost is high and the benefit is marginal.

---

## Execution Order & Dependencies

| Step | Description | Files Changed | Risk | Depends On |
|------|-------------|---------------|------|------------|
| 1 | Fix `Get(key)` → `IQueryable` + `SingleResult` | `DocumentsController.cs` | Low | — |
| 2 | `ExecuteDeleteAsync` + remove dead code | `DocumentsController.cs` | Low | — |
| 3 | Add `Patch` with `Delta<T>` | `DocumentsController.cs`, `LoggingService.cs` | Medium | — |
| 4 | Add `Put` (optional) | `DocumentsController.cs`, `LoggingService.cs` | Medium | Step 3 |
| 5 | Base class to `ControllerBase` | All controllers, `ODataControllerBase.cs` | **High** | — (Deferred) |

Steps 1 and 2 are independent and can be done in parallel. Step 3 can follow immediately. Step 4 is optional. Step 5 is deferred.

## Supporting Changes

### LoggingService additions (for Steps 3–4)

- **File:** `ECTSystem.Api/Logging/LoggingService.cs`
- **Current document EventId range:** 300–311 (311 is `QueryingDocuments`, 300–310 are specific operations)
- **New methods:**
  ```csharp
  [LoggerMessage(EventId = 312, Level = LogLevel.Information, Message = "Patching document {DocumentId}")]
  public partial void PatchingDocument(int documentId);

  [LoggerMessage(EventId = 313, Level = LogLevel.Information, Message = "Document {DocumentId} patched")]
  public partial void DocumentPatched(int documentId);

  [LoggerMessage(EventId = 314, Level = LogLevel.Information, Message = "Updating document {DocumentId}")]
  public partial void UpdatingDocument(int documentId);

  [LoggerMessage(EventId = 315, Level = LogLevel.Information, Message = "Document {DocumentId} updated")]
  public partial void DocumentUpdated(int documentId);
  ```

### Client-side DocumentService (future, not blocking)

- **File:** `ECTSystem.Web/Services/DocumentService.cs`
- Currently has: `GetDocumentsAsync`, `UploadDocumentAsync`, `DeleteDocumentAsync`, `GetForm348PdfAsync`
- Missing: `PatchDocumentAsync`, `UpdateDocumentAsync` — add when client needs to call PATCH/PUT
- Not required for the API-side changes to be functional

## Key Decisions

1. **`ExecuteDeleteAsync` vs `FindAsync` + `Remove`** — Step 2 would be the first `ExecuteDeleteAsync` usage in the codebase. Alternative: keep the current pattern for consistency but remove dead code only.
2. **PUT scope** — Whether to restrict PUT to metadata-only fields (exclude `Content` binary) or allow full entity replacement.
3. **Base class migration** — Whether Phase 3 is worth the disruption given consistent convention routing across all controllers.
