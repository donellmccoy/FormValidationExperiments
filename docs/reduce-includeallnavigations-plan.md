# Implementation Plan: Reduce `IncludeAllNavigations()` Overhead

## Problem Statement

Every mutation endpoint (PATCH, POST) and the single-entity GET in `CasesController` calls `IncludeAllNavigations()`, which issues **11 SQL queries** (1 base + 10 split queries via `.AsSplitQuery()`) to load all navigation properties. This is wasteful because:

- **PATCH**: The client already holds the full object graph in memory. After saving scalar fields, it only needs confirmation of success plus the updated `RowVersion` (for concurrency) and `WorkflowStateHistories` (for `CurrentWorkflowState` derivation).
- **POST**: A newly created case has empty collections, so loading 10 navigation properties returns nothing.
- **TransitionCaseAsync** (client-side): After saving `WorkflowStateHistory` entries via batch, the client re-fetches the entire case with `$expand=FullExpand` just to recompute `CurrentWorkflowState`.

## Current Flow

```
PATCH /odata/Cases({id})
  → delta.Patch(existing)
  → SaveChangesAsync (1 UPDATE query)
  → IncludeAllNavigations() (11 SELECT queries)  ← WASTE
  → return Updated(patched)                       ← 85KB+ JSON

TransitionCaseAsync (client)
  → POST WorkflowStateHistory batch
  → Re-fetch GET /odata/Cases?$filter=Id eq {id}&$expand=<all>  ← DUPLICATE
```

## Target Flow

```
PATCH /odata/Cases({id})
  → delta.Patch(existing)
  → SaveChangesAsync (1 UPDATE query)
  → Re-read scalar entity only (1 SELECT query)
  → return Updated(patched)                       ← ~2KB JSON

TransitionCaseAsync (client)
  → POST WorkflowStateHistory batch
  → Merge saved entries into in-memory case       ← NO re-fetch
```

---

## Implementation Steps

### Phase 1: Slim PATCH Response (Highest Impact) ✅

**Goal**: PATCH returns only scalar properties + `RowVersion`, no navigation properties.

#### Step 1.1: Create `IncludeMinimalNavigations()` extension method ✅
**File**: `ECTSystem.Api/Extensions/LineOfDutyCaseQueryExtensions.cs`

Add a new extension that loads **only** `WorkflowStateHistories` (needed for `CurrentWorkflowState` derivation on the client):

```csharp
public static IQueryable<LineOfDutyCase> IncludeWorkflowState(
    this IQueryable<LineOfDutyCase> query)
{
    return query.Include(c => c.WorkflowStateHistories);
}
```

#### Step 1.2: Update `CasesController.Patch()` to use slim re-read ✅
**File**: `ECTSystem.Api/Controllers/CasesController.cs`

Replace:
```csharp
var patched = await context.Cases
    .IncludeAllNavigations()
    .AsNoTracking()
    .FirstAsync(c => c.Id == key, ct);
```

With:
```csharp
var patched = await context.Cases
    .IncludeWorkflowState()
    .AsNoTracking()
    .FirstAsync(c => c.Id == key, ct);
```

This drops from 11 queries to 2 (base + WorkflowStateHistories).

#### Step 1.3: Update client `SaveCaseAsync` to merge response into existing case ✅
**File**: `ECTSystem.Web/Services/CaseHttpService.cs`

The OData client currently deserializes the PATCH response into a `LineOfDutyCase` object. With a slim response, navigation collections will be null/empty. The client must **merge** the returned scalar fields (especially `RowVersion`) back into the in-memory case rather than replacing it entirely.

**Option A** (Recommended): Change `SaveCaseAsync` to return only the updated `RowVersion` and let the caller keep its in-memory object:

```csharp
public async Task<byte[]> SaveCaseAsync(
    LineOfDutyCase lodCase, CancellationToken cancellationToken = default)
{
    // ... existing PATCH logic ...
    // Return only RowVersion from response for concurrency tracking
}
```

**Option B**: Keep returning `LineOfDutyCase` but merge navigation properties from the existing in-memory copy in `SaveTabFormDataAsync`.

#### Step 1.4: Update `EditCase.razor.cs` `SaveTabFormDataAsync` ✅
**File**: `ECTSystem.Web/Pages/EditCase.razor.cs`

Update to preserve in-memory navigation data after PATCH:

```csharp
// Before: _lineOfDutyCase = await CaseService.SaveCaseAsync(...)
// After:  merge only scalar fields from response
var updatedRowVersion = await CaseService.SaveCaseAsync(_lineOfDutyCase, _cts.Token);
_lineOfDutyCase.RowVersion = updatedRowVersion;
```

This also **eliminates the authority data loss bug** (the saved workaround of capturing `authoritiesToSave` before save becomes unnecessary).

---

### Phase 2: Slim POST Response ✅

**Goal**: POST returns the created case with only essential data.

#### Step 2.1: Update `CasesController.Post()` to use `IncludeWorkflowState()` ✅
**File**: `ECTSystem.Api/Controllers/CasesController.cs`

Replace:
```csharp
var created = await context.Cases
    .IncludeAllNavigations()
    .AsNoTracking()
    .FirstAsync(c => c.Id == lodCase.Id, ct);
```

With:
```csharp
var created = await context.Cases
    .IncludeWorkflowState()
    .AsNoTracking()
    .FirstAsync(c => c.Id == lodCase.Id, ct);
```

For POST this is even less impactful since a new case has no navigation data, but it's consistent and prevents future surprises if navigation seeding is added later.

---

### Phase 3: Eliminate Client-Side Re-fetch After Transition ✅

**Goal**: After `TransitionCaseAsync` saves `WorkflowStateHistory` entries, merge them into the in-memory case instead of re-fetching.

#### Step 3.1: Update `CaseHttpService.TransitionCaseAsync()` to skip re-fetch ✅
**File**: `ECTSystem.Web/Services/CaseHttpService.cs`

Remove the re-fetch query:
```csharp
// REMOVE:
var query = Context.Cases
    .AddQueryOption("$filter", $"Id eq {caseId}")
    .AddQueryOption("$top", 1)
    .AddQueryOption("$expand", FullExpand);
var results = await ExecuteQueryAsync(query, cancellationToken);
```

Instead, return just the saved history entries and let the caller merge them:

```csharp
return new CaseTransitionResponse
{
    HistoryEntries = savedEntries
    // Case property removed or set by caller
};
```

#### Step 3.2: Update `LineOfDutyStateMachine.SaveAndNotifyAsync()` to merge ✅
**File**: `ECTSystem.Web/StateMachines/LineOfDutyStateMachine.cs`

After transition, merge returned `WorkflowStateHistory` entries into `_lineOfDutyCase.WorkflowStateHistories` rather than replacing the entire case object:

```csharp
var response = await _dataService.TransitionCaseAsync(_lineOfDutyCase.Id, request);

foreach (var entry in response.HistoryEntries)
{
    _lineOfDutyCase.WorkflowStateHistories.Add(entry);
}

// CurrentWorkflowState will now correctly derive from the updated collection
```

#### Step 3.3: Update `CaseTransitionResponse` model ✅
**File**: `ECTSystem.Shared/ViewModels/CaseTransitionResponse.cs` (or wherever defined)

Make the `Case` property optional/nullable since it's no longer always populated.

---

### Phase 4: Keep Single GET Unchanged (No Action)

The single-entity GET (`Get(int key)`) should **keep** `IncludeAllNavigations()`. This endpoint serves the initial page load where the client needs the full object graph. It's called once per page visit, and the ETag/304 mechanism prevents redundant loads on re-visits.

---

## Impact Summary

| Operation | Before | After | Savings |
|-----------|--------|-------|---------|
| PATCH (save) | 11 SQL queries, ~85KB response | 2 SQL queries, ~2KB response | **~82% fewer queries, ~97% smaller payload** |
| POST (create) | 11 SQL queries, ~85KB response | 2 SQL queries, ~2KB response | **~82% fewer queries** |
| Transition | 11 API queries + full client re-fetch | 0 extra queries (merge in-memory) | **Eliminates 1 full HTTP round-trip** |
| GET (detail) | No change | No change | — |

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Client data staleness after PATCH | Client already holds current data; only `RowVersion` changes server-side from a PATCH. If another user modified data, the concurrency check would have failed. |
| `CurrentWorkflowState` incorrect after transition | `WorkflowStateHistories` are still included in PATCH response via `IncludeWorkflowState()`, and new entries are merged client-side after transitions. |
| Navigation data needed by tabs after save | Tabs already load their own data (Documents, Tracking) via separate grid `LoadData` events. The initial load via GET still returns everything. |
| Authority data loss bug recurrence | Eliminated — PATCH no longer replaces `_lineOfDutyCase`, so in-memory authority modifications are preserved. |
| OData client deserialization | Must verify that `PanoramicData.OData.Client` handles responses with missing navigation properties gracefully (null collections vs. empty). |

## Testing Plan

1. **Unit tests**: Verify `IncludeWorkflowState()` returns entity with `WorkflowStateHistories` populated and other navigations null.
2. **Integration tests**: PATCH → verify response contains scalar fields + `RowVersion` + `WorkflowStateHistories`, but no `Documents`, `Authorities`, etc.
3. **E2E tests**:
   - Save a form tab → verify no data loss in other tabs.
   - Transition workflow → verify sidebar updates correctly without page reload.
   - Create a new case → verify it loads correctly after creation.
4. **Performance**: Compare SQL Profiler / Application Insights traces before and after to quantify query reduction.

## Implementation Order

1. **Phase 1** (Steps 1.1–1.4) — Slim PATCH response — highest impact, most saves happen via PATCH
2. **Phase 2** (Step 2.1) — Slim POST response — simple, consistent change
3. **Phase 3** (Steps 3.1–3.3) — Eliminate transition re-fetch — second highest impact
4. Phase 4 — No action needed

Phases 1–2 can be done together. Phase 3 requires careful testing of the in-memory merge approach and should be done as a separate PR.
