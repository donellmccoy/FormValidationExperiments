# DTO Implementation Plan

## Scope & Rationale

DTOs are needed for controllers that accept **write payloads** (POST/PUT) using raw entity types. This exposes the API to over-posting, ghost child-entity inserts, and bypassed validation. Read-only endpoints returning `IQueryable<T>` to OData middleware do **not** need response DTOs — OData's `$select`/`$expand` already controls the response shape.

---

## Phase 1: Create Input DTOs in `ECTSystem.Shared/ViewModels/`

Place DTOs in `ECTSystem.Shared` so both API and Blazor WASM client can reference them. Use the existing `ViewModels/` folder.

### 1a. `CreateCaseDto` — for `CasesController.Post`

**Why:** POST currently accepts a raw `LineOfDutyCase` (100+ props, 7 collection navs, `MEDCON = new()`, `INCAP = new()`). This causes ghost MEDCON/INCAP rows and allows over-posting of server-managed fields.

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

### 1b. `CreateMemberDto` / `UpdateMemberDto` — for `MembersController.Post` / `Put`

**Why:** POST/PUT accept raw `Member` with AuditableEntity fields exposed.

```
CreateMemberDto / UpdateMemberDto
├── FirstName (string, required)
├── MiddleInitial (string)
├── LastName (string, required)
├── Rank (enum, required)
├── ServiceNumber (string, required)
├── Unit (string)
├── Component (enum, required)
├── DateOfBirth (DateTime)
```

**Excluded:** `Id` (auto-generated on create; from route on update), `CreatedBy/Date`, `ModifiedBy/Date`.

**Note:** `UpdateMemberDto` is identical in shape but used with PUT. A single `MemberDto` class could serve both, with `Id` supplied via the route parameter.

### 1c. `CreateAuthorityDto` — for `AuthoritiesController.Post`

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

### 1d. `CreateWorkflowStateHistoryDto` — for `WorkflowStateHistoriesController.Post` / `PostBatch`

**Why:** POST accepts raw `WorkflowStateHistory`. Controller already resets 5 auditable fields as "over-posting guard" — a DTO eliminates this entirely.

```
CreateWorkflowStateHistoryDto
├── LineOfDutyCaseId (int, required)
├── WorkflowState (enum, required)
├── Action (enum, required)
├── Status (enum, required)
├── StartDate (DateTime?)
├── EndDate (DateTime?)
├── SignedDate (DateTime?)
├── SignedBy (string)
├── PerformedBy (string)
```

**Excluded:** `Id`, `CreatedBy/Date`, `ModifiedBy/Date`.

### 1e. `CreateBookmarkDto` — for `CaseBookmarksController.Post`

**Why:** POST accepts raw `CaseBookmark` but only uses `LineOfDutyCaseId`.

```
CreateBookmarkDto
├── LineOfDutyCaseId (int, required)
```

**Excluded:** `Id`, `UserId` (set from JWT claims), `BookmarkedDate` (set server-side).

---

## Phase 2: Controllers That Do NOT Need DTOs

| Controller | Reason |
|---|---|
| **DocumentsController** | Read-only (GET only). No writes. |
| **DocumentFilesController** | Already uses `IFormFile` + `[FromForm]` parameters — not entity-based. No change needed. |
| **CasesController (GET)** | Returns `IQueryable<LineOfDutyCase>` for OData composition. OData handles response shaping. |
| **CasesController (PATCH)** | Uses `Delta<LineOfDutyCase>` which is OData's built-in partial-update DTO. Keep as-is — `Delta<T>` already restricts to changed fields only. |
| **AuthoritiesController (PATCH)** | Same — `Delta<LineOfDutyAuthority>` is the DTO. |
| **MembersController (PATCH)** | Same — `Delta<Member>` is the DTO. |
| **CasesController Checkout/Checkin** | No body — key-based actions. |

---

## Phase 3: Add Validation Attributes to DTOs

Move validation from runtime `ModelState.IsValid` checks to declarative attributes:

- `[Required]` on mandatory fields (replaces manual `if (x <= 0) return BadRequest(...)` checks)
- `[StringLength]` on text fields (addresses the missing `HasMaxLength` issue at the API boundary)
- `[Range]` where applicable (e.g., `LineOfDutyCaseId` must be > 0)
- `IValidatableObject` for cross-field rules (e.g., `SubstanceType` required when `WasUnderInfluence == true`)

This lets you **remove** `SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true` for DTO-bound actions. Entity types still need it for OData `Delta<T>` PATCH, so the setting stays global but DTO validation operates independently.

---

## Phase 4: Create Mapper Classes in `ECTSystem.Shared/Mapping/`

Follow the existing `LineOfDutyCaseMapper` pattern — static mapper classes, no third-party library.

```
ECTSystem.Shared/Mapping/
├── LineOfDutyCaseMapper.cs    (existing)
├── CaseDtoMapper.cs           (new: CreateCaseDto → LineOfDutyCase)
├── MemberDtoMapper.cs         (new: CreateMemberDto → Member, UpdateMemberDto → Member)
├── AuthorityDtoMapper.cs      (new: CreateAuthorityDto → LineOfDutyAuthority)
├── WorkflowStateHistoryDtoMapper.cs (new: CreateWorkflowStateHistoryDto → WorkflowStateHistory)
└── BookmarkDtoMapper.cs       (new: CreateBookmarkDto → CaseBookmark)
```

Each mapper has a single `ToEntity()` method that creates the entity with only the DTO-provided fields. Server-managed fields (`Id`, `CreatedBy`, `CreatedDate`, etc.) are left at defaults for EF to populate.

---

## Phase 5: Update Controllers

For each controller, the change pattern is:

1. Change action parameter from `[FromBody] EntityType` to `[FromBody] CreateXxxDto`
2. Replace manual over-posting guards (resetting `Id`, `CreatedBy`, etc.) with mapper call
3. Remove redundant manual validation that's now covered by DTO attributes
4. Return the saved entity (not the DTO) — OData `Created(entity)` needs the entity

**Example — WorkflowStateHistoriesController.Post:**

```csharp
// Before:
public async Task<IActionResult> Post([FromBody] WorkflowStateHistory entry, ...)
{
    entry.Id = 0;                    // over-posting guard
    entry.CreatedBy = string.Empty;  // over-posting guard
    entry.CreatedDate = default;     // over-posting guard
    // ... 3 more resets
    context.WorkflowStateHistories.Add(entry);
}

// After:
public async Task<IActionResult> Post([FromBody] CreateWorkflowStateHistoryDto dto, ...)
{
    var entry = WorkflowStateHistoryDtoMapper.ToEntity(dto);
    context.WorkflowStateHistories.Add(entry);
}
```

---

## Phase 6: Update Blazor WASM Client

The client (`ECTSystem.Web/Services/`) currently sends entity types in POST bodies. Update to send DTOs instead:

- `LineOfDutyCaseHttpService.CreateCaseAsync()` → build `CreateCaseDto` from `LineOfDutyViewModel`
- `MemberHttpService` (if exists) → build `CreateMemberDto`
- `WorkflowStateHistoryHttpService` → build `CreateWorkflowStateHistoryDto`
- `AuthorityHttpService` → build `CreateAuthorityDto`
- `BookmarkHttpService` → build `CreateBookmarkDto`

Since DTOs live in `ECTSystem.Shared`, both projects already reference them.

---

## Phase 7: Update Tests

Update existing controller tests to:

1. Send DTOs instead of entities in POST/PUT test methods
2. Verify that over-posting fields (e.g., `Id`, `CreatedBy`) cannot be set via DTOs (they don't exist on the type)
3. Verify validation attributes fire correctly (e.g., missing `LineOfDutyCaseId` → 400)

---

## Implementation Order

| Step | Effort | Risk |
|------|--------|------|
| 1. Create DTOs (Phase 1) | Low | None — additive |
| 2. Create Mappers (Phase 4) | Low | None — additive |
| 3. Update WorkflowStateHistoriesController | Low | Low — cleanest controller |
| 4. Update CaseBookmarksController | Low | Low — simplest DTO |
| 5. Update AuthoritiesController | Low | Low |
| 6. Update MembersController | Medium | Low |
| 7. Update CasesController (POST only) | Medium | Medium — CaseId generation logic |
| 8. Update Blazor client services (Phase 6) | Medium | Medium — verify OData client compatibility |
| 9. Update tests (Phase 7) | Medium | Low |

---

## What This Fixes

- **Ghost MEDCON/INCAP inserts** — `CreateCaseDto` has no navigation properties
- **Over-posting** — DTOs exclude `Id`, `RowVersion`, audit fields, server-managed flags
- **Missing validation** — DTO attributes provide validation without disabling `SuppressImplicitRequired` globally
- **Cleaner controller code** — eliminates manual field-reset blocks

## What This Does NOT Fix (Separate Work)

- Schema issues (FK defaults, `HasMaxLength`, `HashSet<T>`)
- `Delta<T>` concurrency bypass on PATCH
- Client EDM type safety
- N+1 query patterns (use `$select`/`$expand` or add response DTOs later)
