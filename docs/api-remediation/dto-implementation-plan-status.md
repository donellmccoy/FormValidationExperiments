# DTO Implementation Plan

> **Status: Phases 1–5, 7–8 COMPLETE** (2026-04-16). All DTOs, mappers, controllers, tests, and ETag concurrency implemented and verified — 137/137 tests passing. Phase 6 (Blazor WASM client update) is deferred.

## Scope & Rationale

DTOs are needed for controllers that accept **write payloads** (POST/PUT) using raw entity types. This exposes the API to over-posting, ghost child-entity inserts, and bypassed validation. Read-only endpoints returning `IQueryable<T>` to OData middleware do **not** need response DTOs — OData's `$select`/`$expand` already controls the response shape.

---

## Phase 1: Input DTOs in `ECTSystem.Shared/ViewModels/` ✅ COMPLETE

DTOs live in `ECTSystem.Shared` so both API and Blazor WASM client can reference them, in the existing `ViewModels/` folder.

> **Note:** Several DTOs and mappers listed below already exist. Items marked ✅ need only verification/minor updates; items marked 🆕 are genuinely new work.

### 1a. `CreateCaseDto` — for `CasesController.Post` ✅ DONE

**Why:** POST currently accepts a raw `LineOfDutyCase` (100+ props, 7 collection navs, `MEDCON = new()`, `INCAP = new()`). This causes ghost MEDCON/INCAP rows and allows over-posting of server-managed fields. **(Resolves remediation plan item 9.1.)**

```
CreateCaseDto
├── MemberId (int, required)         — FK to existing Member
├── IncidentType (enum, required)
├── DateOfInjury (DateTime, required)
├── IncidentCircumstances (string, required)
├── ReportedInjury (string, required)
├── DutyStatus (enum)
├── ProcessType (enum)
├── Component (enum)
└── (other initial-intake scalar fields from Items 1–8)
```

**Excluded:** `Id`, `CaseId`, `RowVersion`, `CreatedBy/Date`, `ModifiedBy/Date`, `IsCheckedOut`, `CheckedOutBy*`, all navigation collections, `MEDCON`, `INCAP`, `CurrentWorkflowState`, `WorkflowStateHistories`, `Authorities`, `Documents`, `Notifications`.

**Mapping:** Manual `CreateCaseDto → LineOfDutyCase` factory method in a static `CaseDtoMapper` class (keeps it simple — no AutoMapper dependency needed for a single mapping).

### 1b. `CreateMemberDto` / `UpdateMemberDto` — for `MembersController.Post` / `Put` ✅ already created

**Why:** POST/PUT accept raw `Member` with AuditableEntity fields exposed.

```
CreateMemberDto
├── FirstName (string, required)
├── MiddleInitial (string)
├── LastName (string, required)
├── Rank (string, required)          — string, not enum
├── ServiceNumber (string, required)
├── Unit (string)
├── Component (enum, required)
├── DateOfBirth (DateTime?)           — nullable

UpdateMemberDto (same fields plus concurrency token)
├── (all CreateMemberDto fields)
├── RowVersion (byte[], required)    — needed for optimistic concurrency on PUT
```

**Excluded:** `Id` (auto-generated on create; from route on update), `CreatedBy/Date`, `ModifiedBy/Date`.

**Note:** `UpdateMemberDto` adds `RowVersion` for concurrency. `MembersController` uses **PUT** (not PATCH) with `SetValues` and audit-field protection (`CreatedBy/Date`, `ModifiedBy/Date` marked `IsModified = false`).

### 1c. `CreateAuthorityDto` — for `AuthoritiesController.Post` ✅ already created

**Why:** POST accepts raw `LineOfDutyAuthority`. The controller already manually validates `LineOfDutyCaseId` — a DTO makes this declarative via `[Required]`.

```
CreateAuthorityDto
├── LineOfDutyCaseId (int, required)
├── Role (string, required)
├── Name (string, required)
├── Rank (string)
├── Title (string)
├── ActionDate (DateTime?)
├── Recommendation (string)
├── Comments (List<string>)
```

**Excluded:** `Id`, `CreatedBy/Date`, `ModifiedBy/Date`.

### 1d. `CreateWorkflowStateHistoryDto` — for `WorkflowStateHistoriesController.Post` / `PostBatch` ✅ already created

**Why:** POST accepts raw `WorkflowStateHistory` with no over-posting guards — a DTO eliminates this risk.

```
CreateWorkflowStateHistoryDto
├── LineOfDutyCaseId (int, required)
├── WorkflowState (enum, required)
├── EnteredDate (DateTime, required)
├── ExitDate (DateTime?)
```

**Note:** The `WorkflowStateHistory` entity has only these 4 domain fields (plus `AuditableEntity` base). The PATCH endpoint only allows mutation of `ExitDate`.

**Excluded:** `Id`, `CreatedBy/Date`, `ModifiedBy/Date`.

### 1e. `CreateBookmarkDto` — for `BookmarksController.Post` ✅ already created

**Why:** POST accepts raw `Bookmark` but only uses `LineOfDutyCaseId`.

```
CreateBookmarkDto
├── LineOfDutyCaseId (int, required)
```

**Excluded:** `Id`, `UserId` (set from JWT claims), audit fields (`CreatedBy/Date`, `ModifiedBy/Date`).

### 1f. `UpdateCaseDto` — for `CasesController.Patch` ✅ DONE

**Why:** PATCH previously used `Delta<LineOfDutyCase>`, which allowed clients to submit changes to any of the entity's 100+ properties — including server-managed fields (`IsDeleted`, `IsCheckedOut`, `CheckedOutBy`, `CheckedOutDate`, `CurrentWorkflowState`, audit fields). Using `UpdateCaseDto` restricts patchable fields at the type level. **(Resolves remediation plan item 9.2.)**

> **Implementation divergence:** The plan originally proposed `Delta<UpdateCaseDto>` with `ToUpdateDto()` round-tripping. The actual implementation accepts `UpdateCaseDto` directly (no `Delta<T>` wrapper) and applies changes via `CaseDtoMapper.ApplyUpdate(dto, entity)`. This is simpler — avoids EDM registration complexity for a DTO type — and achieves the same over-posting protection since the DTO excludes all server-managed fields. `If-Match` / ETag concurrency is implemented per Phase 8.

```
UpdateCaseDto
├── IncidentType (enum)
├── DateOfInjury (DateTime)
├── IncidentCircumstances (string)
├── ReportedInjury (string)
├── DutyStatus (enum)
├── ProcessType (enum)
├── Component (enum)
├── WasUnderInfluence (bool?)
├── SubstanceType (enum?)
├── ToxicologyResults (string)
├── (other client-editable scalar fields from AF Form 348)
```

**Excluded:** `Id`, `CaseId`, `RowVersion`, `CreatedBy/Date`, `ModifiedBy/Date`, `IsDeleted`, `IsCheckedOut`, `CheckedOutBy`, `CheckedOutDate`, `CurrentWorkflowState`, all navigation collections, `MEDCON`, `INCAP`.

**Actual Controller Pattern (implemented):**

```csharp
public async Task<IActionResult> Patch([FromODataUri] int key, [FromBody] UpdateCaseDto dto, CancellationToken ct)
{
    // ... If-Match header validation (Phase 8) ...
    var existing = await context.Cases.FindAsync([key], ct);
    if (existing is null) return NotFound();
    context.Entry(existing).Property(e => e.RowVersion).OriginalValue = clientRowVersion;
    CaseDtoMapper.ApplyUpdate(dto, existing);
    await context.SaveChangesAsync(ct);  // catches DbUpdateConcurrencyException → 409
    var patched = await context.Cases.IncludeWorkflowState().AsNoTracking().FirstAsync(c => c.Id == key, ct);
    Response.Headers.ETag = $"\"{Convert.ToBase64String(patched.RowVersion)}\"";
    return Updated(patched);
}
```

**Note:** `AuthoritiesController.Patch` and `MembersController.Patch` still use `Delta<Entity>`. If those entities gain sensitive server-managed fields beyond `AuditableEntity`, add corresponding Update DTOs.

---

## Phase 2: Controllers That Do NOT Need DTOs (no changes required)

| Controller | Reason |
|---|---|
| **DocumentsController** | Read-only (GET only). No writes. |
| **DocumentFilesController** | Already uses `IFormFile` + `[FromForm]` parameters — not entity-based. No change needed. |
| **CasesController (GET)** | Returns `IQueryable<LineOfDutyCase>` for OData composition. OData handles response shaping. |
| **CasesController (PATCH)** | ~~Keep as-is~~ → **Now uses `UpdateCaseDto` directly** (see Phase 1f). `Delta<LineOfDutyCase>` exposed server-managed fields to over-posting. |
| **AuthoritiesController (PATCH)** | Uses `Delta<LineOfDutyAuthority>` — acceptable for now; audit fields are the only exposure. Add `UpdateAuthorityDto` if the entity gains server-managed flags. |
| **MembersController (PUT)** | Uses `SetValues` with audit-field protection (`IsModified = false`); acceptable for now. No `Delta<T>`. |
| **CasesController Checkout/Checkin** | Uses `ODataActionParameters` — key + optional `RowVersion` only; no entity binding. |

---

## Phase 3: Add Validation Attributes to DTOs ✅ COMPLETE ✅ COMPLETE

Move validation from runtime `ModelState.IsValid` checks to declarative attributes:

- `[Required]` on mandatory fields (replaces manual `if (x <= 0) return BadRequest(...)` checks)
- `[StringLength]` on text fields (addresses the missing `HasMaxLength` issue at the API boundary)
- `[Range]` where applicable (e.g., `LineOfDutyCaseId` must be > 0)
- `IValidatableObject` for cross-field rules (e.g., `SubstanceType` required when `WasUnderInfluence == true`)

This lets you **remove** `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` for DTO-bound actions. Entity types still need it for OData `Delta<T>` PATCH, so the setting stays global but DTO validation operates independently.

---

## Phase 4: Mapper Classes in `ECTSystem.Shared/Mapping/` ✅ COMPLETE

Follow the existing `LineOfDutyCaseMapper` pattern — static mapper classes, no third-party library.

```
ECTSystem.Shared/Mapping/
├── LineOfDutyCaseMapper.cs              (existing)
├── CaseDtoMapper.cs                     (✅ DONE — ToEntity + ApplyUpdate)
├── MemberDtoMapper.cs                   (✅ existing: ToEntity + ApplyUpdate with RowVersion)
├── AuthorityDtoMapper.cs                (✅ existing: ToEntity)
├── WorkflowStateHistoryDtoMapper.cs     (✅ existing: ToEntity)
└── BookmarkDtoMapper.cs                 (✅ existing: ToEntity → Bookmark)
```

Each mapper has a `ToEntity()` method that creates the entity with only the DTO-provided fields. Server-managed fields (`Id`, `CreatedBy`, `CreatedDate`, etc.) are left at defaults for EF to populate. `MemberDtoMapper` also has `ApplyUpdate()` which copies `RowVersion` for concurrency. `CaseDtoMapper` has `ToEntity()` and `ApplyUpdate(UpdateCaseDto, LineOfDutyCase)` — no `ToUpdateDto()` needed since the actual implementation accepts `UpdateCaseDto` directly (not `Delta<UpdateCaseDto>`).

---

## Phase 5: Update Controllers ✅ COMPLETE

For each controller, the change pattern is:

1. Change action parameter from `[FromBody] EntityType` to `[FromBody] CreateXxxDto`
2. Replace manual over-posting guards (resetting `Id`, `CreatedBy`, etc.) with mapper call
3. Remove redundant manual validation that's now covered by DTO attributes
4. Return the saved entity (not the DTO) — OData `Created(entity)` needs the entity

**Example — WorkflowStateHistoriesController.Post:**

```csharp
// Before (no over-posting guards — raw entity passed straight to EF):
public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, ...)
{
    if (!ModelState.IsValid) return ValidationProblem(ModelState);
    context.WorkflowStateHistories.Add(entry);   // client can set Id, audit fields, etc.
    await context.SaveChangesAsync(ct);
    return Created(entry);
}

// After:
public async Task<IActionResult> Post([FromBody] CreateWorkflowStateHistoryDto dto, ...)
{
    if (!ModelState.IsValid) return ValidationProblem(ModelState);
    var entry = WorkflowStateHistoryDtoMapper.ToEntity(dto);
    context.WorkflowStateHistories.Add(entry);
    await context.SaveChangesAsync(ct);
    return Created(entry);
}
```

---

## Phase 6: Update Blazor WASM Client ⏳ DEFERRED

> **Status:** Deferred. Server-side DTO enforcement is complete. Client-side service updates to send DTOs (instead of raw entities) are tracked as future work. The OData client currently still sends entity types via `Context.AddObject`/`UpdateObject`.

The client (`ECTSystem.Web/Services/`) currently sends entity types via the `Microsoft.OData.Client` (`Context.AddObject` / `Context.UpdateObject` / `Context.SaveChangesAsync`). Update to send DTOs instead:

- `CaseService.SaveCaseAsync()` → build `CreateCaseDto` from `LineOfDutyViewModel` (for new cases)
- `MemberService` → build `CreateMemberDto` / `UpdateMemberDto`
- `WorkflowHistoryService` → build `CreateWorkflowStateHistoryDto`
- `AuthorityService` → build `CreateAuthorityDto`
- `BookmarkService` → build `CreateBookmarkDto`

Since DTOs live in `ECTSystem.Shared`, both projects already reference them.

**Note:** The OData client uses `AddObject`/`UpdateObject` + `SaveChangesAsync`, not raw `HttpClient` POST. DTO objects will need to be registered in the client EDM model or sent via direct `HttpClient` calls alongside the OData context.

---

## Phase 7: Update Tests ✅ COMPLETE

Updated existing controller tests to:

1. Send DTOs instead of entities in POST/PUT test methods
2. Verify that over-posting fields (e.g., `Id`, `CreatedBy`) cannot be set via DTOs (they don't exist on the type)
3. Verify validation attributes fire correctly (e.g., missing `LineOfDutyCaseId` → 400)

**Implementation details:**
- Fixed CS1503 errors across all 5 controller test files (test methods now construct DTOs instead of entities)
- Fixed CS0117 errors where tests referenced entity-only properties not on DTOs
- Fixed RowVersion null handling for InMemoryDatabase (set non-null default in `TestDbContext.SeedData`)
- All **137/137 tests passing** after implementation

---

## Phase 8: ETag / If-Match Concurrency Enforcement on PATCH ✅ COMPLETE

**Why:** Once `UpdateCaseDto` excludes `RowVersion`, the current concurrency mechanism (RowVersion travels inside the PATCH delta body, then is used as `OriginalValue`) stops working. The fix is to require an `If-Match` header carrying the ETag (Base64-encoded `RowVersion`) — the same format the GET endpoint already returns.

### 8a. Controller — Read `If-Match`, reject if missing

**File:** `Controllers/CasesController.cs`

```csharp
public async Task<IActionResult> Patch([FromODataUri] int key, Delta<UpdateCaseDto> delta, CancellationToken ct = default)
{
    // ... validation ...

    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var existing = await context.Cases.FindAsync([key], ct);
    if (existing is null) return NotFound();

    // ── ETag concurrency check ──
    var ifMatch = Request.Headers.IfMatch.ToString();
    if (string.IsNullOrEmpty(ifMatch))
    {
        return Problem(
            title: "Precondition required",
            detail: "If-Match header with ETag is required for PATCH.",
            statusCode: StatusCodes.Status428PreconditionRequired);
    }

    byte[] clientRowVersion;
    try
    {
        clientRowVersion = Convert.FromBase64String(ifMatch.Trim('"'));
    }
    catch (FormatException)
    {
        return Problem(
            title: "Invalid ETag",
            detail: "If-Match header contains an invalid ETag value.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    // Apply DTO changes
    var dto = CaseDtoMapper.ToUpdateDto(existing);
    delta.Patch(dto);
    CaseDtoMapper.ApplyUpdate(dto, existing);

    // Set OriginalValue to client's RowVersion for optimistic concurrency
    context.Entry(existing).Property(e => e.RowVersion).OriginalValue = clientRowVersion;

    try
    {
        await context.SaveChangesAsync(ct);
    }
    catch (DbUpdateConcurrencyException)
    {
        return Problem(
            title: "Concurrency conflict",
            detail: "The entity was modified by another user. Refresh and retry.",
            statusCode: StatusCodes.Status409Conflict);
    }

    var patched = await context.Cases.IncludeWorkflowState().AsNoTracking().FirstAsync(c => c.Id == key, ct);
    Response.Headers.ETag = $"\"{ Convert.ToBase64String(patched.RowVersion)}\"";
    return Updated(patched);
}
```

**Key points:**
- `428 Precondition Required` if `If-Match` is missing — forces clients to supply a concurrency token.
- `400 Bad Request` if the ETag value isn't valid Base64.
- `409 Conflict` on stale RowVersion (existing behavior, now fed from the header).
- Response includes updated `ETag` header so the client has the fresh token for subsequent PATCHes.

### 8b. Client — Send `If-Match` header on PATCH

**File:** `ECTSystem.Web/Services/CaseService.cs`

The OData client's `UpdateObject` / `SaveChangesAsync` flow doesn't natively set `If-Match`. Attach the header in `SendingRequest2`:

```csharp
// Before SaveChangesAsync:
Context.SendingRequest2 += (_, e) =>
{
    if (e.RequestMessage.Method == "PATCH")
    {
        var etag = $"\"{ Convert.ToBase64String(lodCase.RowVersion)}\"";
        e.RequestMessage.SetHeader("If-Match", etag);
    }
};
await Context.SaveChangesAsync(cancellationToken);
```

Alternatively, if migrating to `HttpClient`-based PATCH calls, set the header directly:

```csharp
request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{ Convert.ToBase64String(lodCase.RowVersion)}\""));
```

### 8c. Apply same pattern to other PATCH endpoints (optional)

If `AuthoritiesController.Patch` or `MembersController.Patch` later adopt `Delta<UpdateXxxDto>`, apply the same `If-Match` pattern. For now, those controllers still use `Delta<Entity>` with RowVersion in the delta body, so the existing mechanism continues to work.

---

## Implementation Order

| Step | Effort | Risk | Status |
|------|--------|------|--------|
| 1. Create DTOs (Phase 1) | Low | None — additive | ✅ Done |
| 2. Create Mappers (Phase 4) | Low | None — additive | ✅ Done |
| 3. Update WorkflowStateHistoriesController | Low | Low — cleanest controller | ✅ Done |
| 4. Update BookmarksController | Low | Low — simplest DTO | ✅ Done |
| 5. Update AuthoritiesController | Low | Low | ✅ Done |
| 6. Update MembersController | Medium | Low | ✅ Done |
| 7. Update CasesController (POST) | Medium | Medium — CaseId generation logic | ✅ Done |
| 8. Update CasesController (PATCH → `UpdateCaseDto`) | Medium | Medium — mapper | ✅ Done |
| 9. Wire `If-Match` ETag on PATCH (Phase 8) | Medium | Medium — client must send header; breaks clients that omit it | ✅ Done |
| 10. Update Blazor client services (Phase 6) | Medium | Medium — verify OData client compatibility | ⏳ Deferred |
| 11. Update tests (Phase 7) | Medium | Low | ✅ Done |

---

## What This Fixes

- **Ghost MEDCON/INCAP inserts** — `CreateCaseDto` has no navigation properties
- **Over-posting on POST** — Create DTOs exclude `Id`, `RowVersion`, audit fields, server-managed flags **(remediation item 9.1)**
- **Over-posting on PATCH** — `UpdateCaseDto` restricts patchable fields at the type level; server-managed properties don't exist on the DTO **(remediation item 9.2)**
- **ETag/RowVersion concurrency on PATCH** — `If-Match` header replaces in-body RowVersion; `428` enforces that clients always supply a concurrency token **(Phase 8)**
- **Missing validation** — DTO attributes provide validation without disabling `SuppressImplicitRequired` globally
- **Cleaner controller code** — eliminates manual field-reset blocks

## What This Does NOT Fix (Separate Work)

- Schema issues (FK defaults, `HasMaxLength`, `HashSet<T>`) → **Resolved.** `ConstrainNvarcharMaxColumns` migration applied `HasMaxLength` constraints across all string columns. `HashSet<T>` is intentional (documented in `entity-model-design-review.md`). FK configurations use explicit `HasOne`/`HasMany` with proper delete behaviors (`ClientCascade` for collections, `NoAction` for MEDCON/INCAP/Member). Minor remaining item: `List<string>` for `Authority.Comments` uses `StringListConversion` — tracked in entity-model-design-review.md.
- `Delta<T>` concurrency/ETag enforcement on PATCH → **Resolved in Phase 8** (`If-Match` header with `428 Precondition Required`)
- Client EDM type safety → **Known risk, low severity.** `BuildClientEdmModel()` in `ServiceCollectionExtensions.cs` only registers `Id` keys + enum properties per entity type; ~90% of scalar properties are unregistered (still materialize via OData convention). Main risk is drift: adding a new enum property to a model without updating the client EDM causes a runtime `NullReferenceException`. Mitigation: add a unit test that compares client `EdmModel` entity types against server entity types.
- N+1 query patterns (use `$select`/`$expand` or add response DTOs later) → **Server-side N+1 resolved.** Single-entity GET refactored to `SingleResult.Create()` (Concern #4 in `odata-concerns-2-3-4-plan.md`). `IncludeAllNavigations()` removed from hot path. `AsSplitQuery()` prevents cartesian products. Remaining work is **client-side payload optimization** (redundant tab fetches, over-expanded initial case load) — tracked in `future-optimizations.md` items #1 and #4.
