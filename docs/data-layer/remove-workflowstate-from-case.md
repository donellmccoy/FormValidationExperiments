# Remove `WorkflowState` from `LineOfDutyCase` — Redesign Plan

## Goal

Remove the `WorkflowState` property from `LineOfDutyCase` and derive the current workflow state from the most recent `WorkflowStateHistory` entry (by `CreatedDate`, with `Id` as a tiebreaker). The `WorkflowStateHistories` table becomes the single source of truth for a case's workflow state.

---

## Design Principle

**Current state = the `WorkflowState` value on the most-recent `WorkflowStateHistory` row for a given `LineOfDutyCaseId`, ordered by `CreatedDate DESC, Id DESC`.**

There is no denormalized `WorkflowState` column on `LineOfDutyCase`.

---

## Phase 1 — Shared Model & Extensions

### 1.1 Remove `WorkflowState` from `LineOfDutyCase`

**File:** `ECTSystem.Shared/Models/LineOfDutyCase.cs` (line 15)

- Delete `public WorkflowState WorkflowState { get; set; } = WorkflowState.Draft;`
- Add a read-only computed property that derives state from the collection:

```csharp
/// <summary>
/// Derives the current workflow state from the most recent WorkflowStateHistory entry.
/// Returns <see cref="WorkflowState.Draft"/> when no history exists.
/// </summary>
public WorkflowState CurrentWorkflowState =>
    WorkflowStateHistories?
        .OrderByDescending(h => h.CreatedDate)
        .ThenByDescending(h => h.Id)
        .FirstOrDefault()?.WorkflowState ?? WorkflowState.Draft;
```

> **Important:** This property works only when `WorkflowStateHistories` is loaded. All query paths that need the current state must `$expand=WorkflowStateHistories` or use a server-side computed column/query (see Phase 3).

### 1.2 Remove `UpdateWorkflowState` extension method

**File:** `ECTSystem.Web/Extensions/LineOfDutyExtensions.cs`

- Delete the entire `UpdateWorkflowState` extension method. It directly sets `lineOfDutyCase.WorkflowState`, which will no longer exist.
- If the file becomes empty, delete it.

### 1.3 Update `AddInitialHistory` and `AddSignedHistory`

**File:** `ECTSystem.Shared/Extensions/LineOfDutyExtensions.cs`

Both methods currently read `lodCase.WorkflowState` to pass into the factory:

```csharp
// Before
WorkflowStateHistoryFactory.CreateInitialHistory(lodCase.Id, lodCase.WorkflowState, ...)
WorkflowStateHistoryFactory.CreateSigned(lodCase.Id, lodCase.WorkflowState, ...)
```

Change these to accept an explicit `WorkflowState` parameter:

```csharp
public static void AddInitialHistory(this LineOfDutyCase lodCase, WorkflowState state, DateTime? startDate = null)
{
    lodCase.AddHistoryEntry(
        WorkflowStateHistoryFactory.CreateInitialHistory(lodCase.Id, state, startDate ?? lodCase.CreatedDate));
}

public static void AddSignedHistory(this LineOfDutyCase lodCase, WorkflowState state,
    DateTime? stepStartDate, DateTime? signedDate, string signedBy)
{
    lodCase.AddHistoryEntry(
        WorkflowStateHistoryFactory.CreateSigned(lodCase.Id, state, stepStartDate, signedDate, signedBy));
}
```

### 1.4 Update `CaseTransitionRequest`

**File:** `ECTSystem.Shared/Models/CaseTransitionRequest.cs`

- Remove the `NewWorkflowState` property. The target state is already captured in the history entries themselves. The server will derive the current state from the most recent history entry after persisting.

```csharp
public class CaseTransitionRequest
{
    /// <summary>The workflow state history entries to persist for this transition.</summary>
    public List<WorkflowStateHistory> HistoryEntries { get; set; } = [];
}
```

### 1.5 Update `LineOfDutyCaseMapper`

**File:** `ECTSystem.Shared/Mapping/LineOfDutyCaseMapper.cs` (line ~50)

Change:
```csharp
WorkflowState = source.WorkflowState,
```
To:
```csharp
WorkflowState = source.CurrentWorkflowState,
```

### 1.6 Update `LineOfDutyViewModel`

**File:** `ECTSystem.Shared/ViewModels/LineOfDutyViewModel.cs` (line 45)

No change needed — the `WorkflowState` property on the ViewModel stays. It represents a display value, not a persisted column. It will be populated from `CurrentWorkflowState` via the mapper.

---

## Phase 2 — State Machine (Blazor WASM)

### 2.1 Update `LineOfDutyStateMachine` constructors

**File:** `ECTSystem.Web/StateMachines/LineOfDutyStateMachine.cs`

**Constructor with existing case (~line 217):**

```csharp
// Before
_sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(lineOfDutyCase.WorkflowState, FiringMode.Queued);

// After
_sm = new StateMachine<WorkflowState, LineOfDutyTrigger>(lineOfDutyCase.CurrentWorkflowState, FiringMode.Queued);
```

**Constructor for new case (~line 242):** No change — already uses `WorkflowState.Draft` directly.

### 2.2 Update `SaveAndNotifyAsync`

**File:** `ECTSystem.Web/StateMachines/LineOfDutyStateMachine.cs` (~lines 125–195)

| Current code | Replacement |
|---|---|
| `var previousState = _lineOfDutyCase.WorkflowState;` | `var previousState = _lineOfDutyCase.CurrentWorkflowState;` |
| `_lineOfDutyCase.UpdateWorkflowState(targetState);` | Remove — state is no longer set directly; it will be derived after history entries are persisted |
| `_lineOfDutyCase.WorkflowState = previousState;` (rollback in catch block) | Remove — no property to roll back. On error, the history entries were never saved, so `CurrentWorkflowState` still returns `previousState` |
| `WorkflowTabHelper.GetTabIndexForState(saved.WorkflowState)` | `WorkflowTabHelper.GetTabIndexForState(saved.CurrentWorkflowState)` |

### 2.3 Update `CaseTransitionRequest` construction in `SaveAndNotifyAsync`

Remove `NewWorkflowState = targetState` from the request construction (aligns with Phase 1.4).

### 2.4 Update `TransitionCaseAsync` in `CaseHttpService`

**File:** `ECTSystem.Web/Services/CaseHttpService.cs` (~lines 135–160)

Remove the PATCH to update `WorkflowState`:

```csharp
// Before
var patchContent = JsonContent.Create(
    new { WorkflowState = request.NewWorkflowState }, options: ODataJsonOptions);
var patchResponse = await HttpClient.PatchAsync($"odata/Cases({caseId})", patchContent, cancellationToken);

// After — just POST the history entries, no PATCH to the case needed
```

The method should:
1. POST each `WorkflowStateHistory` entry from `request.HistoryEntries`
2. GET the updated case (with `$expand=WorkflowStateHistories`) to return the refreshed entity

Update `CaseTransitionResponse` accordingly — it returns the refreshed case with its history.

### 2.5 Update `BuildScalarPatchBody` in `CaseHttpService`

**File:** `ECTSystem.Web/Services/CaseHttpService.cs`

Remove `WorkflowState` from the scalar patch body if it is currently included.

---

## Phase 3 — API Layer

### 3.1 Remove `WorkflowState` from OData EDM

**File:** `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` (or wherever the OData model is configured)

If `WorkflowState` is exposed as a structural property on the `LineOfDutyCase` entity type, remove it. The property will no longer exist on the model.

### 3.2 Update `CasesController`

**File:** `ECTSystem.Api/Controllers/CasesController.cs`

- Remove any PATCH handling that sets `WorkflowState` on the entity.
- If there's a dedicated transition endpoint, update it to only persist history entries (the PATCH to set `WorkflowState` on the case row is no longer needed).
- The `GetWorkflowStateHistories` navigation property endpoint (line 375–384) remains unchanged.

### 3.3 Add a server-side computed property or OData function for filtering

**Problem:** Dashboard OData queries like `$filter=WorkflowState eq 'UnitCommanderReview'` rely on `WorkflowState` being a filterable database column. After removing the column, these queries break.

**Options (choose one):**

#### Option A — Database computed column (recommended for OData filtering)

Add a **persisted computed column** or a **database view** that materializes the current workflow state:

```sql
-- SQL computed column (not persisted — calculated at query time)
ALTER TABLE LineOfDutyCases
ADD CurrentWorkflowState AS (
    SELECT TOP 1 WorkflowState
    FROM WorkflowStateHistories
    WHERE LineOfDutyCaseId = LineOfDutyCases.Id
    ORDER BY CreatedDate DESC, Id DESC
);
```

> **Note:** SQL Server does not support subqueries in computed columns. Use a **scalar function** + computed column, or a **view/indexed view** instead.

**Preferred approach — scalar UDF + computed column:**

```sql
CREATE FUNCTION dbo.fn_CurrentWorkflowState(@CaseId INT)
RETURNS INT
AS
BEGIN
    RETURN (
        SELECT TOP 1 WorkflowState
        FROM WorkflowStateHistories
        WHERE LineOfDutyCaseId = @CaseId
        ORDER BY CreatedDate DESC, Id DESC
    );
END;

ALTER TABLE LineOfDutyCases
ADD CurrentWorkflowState AS dbo.fn_CurrentWorkflowState(Id);
```

Then map in EF Core:
```csharp
entity.Property(e => e.CurrentWorkflowState)
    .HasComputedColumnSql("dbo.fn_CurrentWorkflowState(Id)");
```

This makes `CurrentWorkflowState` a queryable column for OData `$filter` without denormalization.

#### Option B — Maintain a shadow/denormalized column

Keep a `CurrentWorkflowState` column on `LineOfDutyCases` but treat it as a **server-managed denormalized cache** updated by a database trigger or by the API transition endpoint. This is faster for reads but introduces dual-write risk.

#### Option C — Filter via `$expand` + `$filter` on the client

Rewrite Dashboard queries to filter by the `WorkflowStateHistories` navigation property:

```
/odata/Cases?$expand=WorkflowStateHistories&$filter=WorkflowStateHistories/any(h: h/WorkflowState eq 'UnitCommanderReview' and h/Status eq 'InProgress')
```

> **Downside:** More complex queries, may not perform well on large datasets without careful indexing.

**Recommendation:** Use **Option A** (scalar UDF + computed column) for the cleanest separation. It preserves OData filterability, requires no client-side query changes beyond renaming `WorkflowState` to `CurrentWorkflowState`, and keeps the history table as the single source of truth.

---

## Phase 4 — UI Components

### 4.1 Update `WorkflowSidebar.razor.cs`

**File:** `ECTSystem.Web/Shared/WorkflowSidebar.razor.cs` (line ~96)

```csharp
// Before
var rawState = lodCase is not null ? (int)lodCase.WorkflowState : 1;

// After
var rawState = lodCase is not null ? (int)lodCase.CurrentWorkflowState : 1;
```

### 4.2 Update `EditCase.razor.cs`

**File:** `ECTSystem.Web/Pages/EditCase.razor.cs`

All references to `_lineOfDutyCase?.WorkflowState` become `_lineOfDutyCase?.CurrentWorkflowState` (or `.CurrentWorkflowState ?? WorkflowState.Draft`):

| Line area | Current | Replacement |
|---|---|---|
| ~284 | `_lineOfDutyCase?.WorkflowState ?? WorkflowState.Draft` | `_lineOfDutyCase?.CurrentWorkflowState ?? WorkflowState.Draft` |
| ~341 | Same pattern | Same replacement |
| ~382 | Same pattern | Same replacement |
| ~1034 (`IsTabDisabled`) | `_lineOfDutyCase?.WorkflowState` | `_lineOfDutyCase?.CurrentWorkflowState` |
| ~438 (OData select) | Remove `WorkflowState` from `$select` | N/A — it's no longer a column |

### 4.3 Update `Dashboard.razor.cs`

**File:** `ECTSystem.Web/Pages/Dashboard.razor.cs`

If using Option A (computed column), rename filter references:
```csharp
// Before
$"WorkflowState eq '{WorkflowState.UnitCommanderReview}'"
$"WorkflowState ne '{WorkflowState.Completed}'"

// After  
$"CurrentWorkflowState eq '{WorkflowState.UnitCommanderReview}'"
$"CurrentWorkflowState ne '{WorkflowState.Completed}'"
```

### 4.4 Update `CaseList.razor` and `MyBookmarks.razor`

Any column bindings or display logic referencing `.WorkflowState` become `.CurrentWorkflowState`.

### 4.5 Update `WorkflowTabHelper`

**File:** `ECTSystem.Web/Helpers/WorkflowTabHelper.cs`

No structural changes needed — the helper accepts a `WorkflowState` enum value. Callers just need to pass `CurrentWorkflowState` instead of `WorkflowState`.

---

## Phase 5 — Entity Framework & Database

### 5.1 Update EF configuration

**File:** `ECTSystem.Persistence/Data/Configurations/WorkflowStateHistoryConfiguration.cs`

Add an index on `CreatedDate` for performance (the current composite index is on `LineOfDutyCaseId, WorkflowState`):

```csharp
// Existing index — keep for history lookups by state
builder.HasIndex(e => new { e.LineOfDutyCaseId, e.WorkflowState });

// New index — optimized for "most recent entry" queries
builder.HasIndex(e => new { e.LineOfDutyCaseId, e.CreatedDate, e.Id })
    .IsDescending(false, true, true);
```

### 5.2 Remove `WorkflowState` from `LineOfDutyCase` EF mapping

If there is an explicit EF configuration for `LineOfDutyCase.WorkflowState`, remove it. The `CurrentWorkflowState` computed property should be:

- **If using Option A (computed column):** Map with `.HasComputedColumnSql(...)` and mark as `.ValueGeneratedOnAddOrUpdate()`
- **If using the C# LINQ property:** Mark with `[NotMapped]` or `.Ignore(e => e.CurrentWorkflowState)` in the EF configuration, since it is purely client-computed from the loaded collection

### 5.3 Create EF migration

```bash
dotnet ef migrations add RemoveWorkflowStateFromCase --project ECTSystem.Persistence --startup-project ECTSystem.Api
```

This migration will:
1. Drop the `WorkflowState` column from `LineOfDutyCases`
2. (Option A) Create the `fn_CurrentWorkflowState` scalar function and add the computed column

### 5.4 Update `EctDbSeeder`

**File:** `ECTSystem.Persistence/Data/EctDbSeeder.cs` (line ~92)

- Remove `WorkflowState = workflowState` from the `LineOfDutyCase` initializer
- Ensure the seeder creates a `WorkflowStateHistory` entry matching the intended initial state (it already does this at line ~192)
- The seeded case's `CurrentWorkflowState` will be automatically derived from the seeded history entry

---

## Phase 6 — Tests

### 6.1 Update test data factories

All test helpers that construct `LineOfDutyCase` with `WorkflowState = ...` must instead seed a `WorkflowStateHistory` entry:

```csharp
// Before
var testCase = new LineOfDutyCase { WorkflowState = WorkflowState.MedicalTechnicianReview };

// After
var testCase = new LineOfDutyCase
{
    WorkflowStateHistories = new List<WorkflowStateHistory>
    {
        WorkflowStateHistoryFactory.CreateInitialHistory(0, WorkflowState.MedicalTechnicianReview)
    }
};
// testCase.CurrentWorkflowState now returns MedicalTechnicianReview
```

### 6.2 Files to update

| Test file | Changes |
|---|---|
| `LineOfDutyStateMachineTests.cs` | Replace `WorkflowState = ...` on test cases with history entries; assert on `CurrentWorkflowState` |
| `LineOfDutyExtensionsTests.cs` | Update `AddInitialHistory`/`AddSignedHistory` tests to pass explicit `WorkflowState` param; remove `UpdateWorkflowState` tests |
| `WorkflowStateHistoriesControllerTests.cs` | Minor — mostly works on history entries directly |
| `LineOfDutyCaseMapperTests.cs` | Seed history entry instead of setting `WorkflowState` |

---

## Implementation Order

| Step | Phase | Risk | Notes |
|---|---|---|---|
| 1 | 1.1 – 1.5 | Medium | Core model change — breaks compilation everywhere |
| 2 | 2.1 – 2.5 | High | State machine is the most complex consumer |
| 3 | 3.1 – 3.3 | Medium | Must decide on Option A/B/C for filtering |
| 4 | 4.1 – 4.5 | Low | Mechanical find-and-replace |
| 5 | 5.1 – 5.4 | Medium | Database migration — irreversible in production |
| 6 | 6.1 – 6.2 | Low | Update all tests to compile and pass |

**Recommended approach:** Implement Phases 1–6 in a single branch. The model change (Phase 1) will break compilation across the entire solution, so all downstream phases must be completed before the build succeeds. Run all tests after Phase 6 to validate.

---

## Risk & Considerations

1. **Performance:** `CurrentWorkflowState` via LINQ-to-Objects on the loaded collection is O(n) per access. For cases with many history entries, this is negligible. For OData `$filter` queries against the database, use the computed column (Option A) to avoid N+1 issues.

2. **Consistency:** With the denormalized column gone, there is zero risk of `WorkflowState` drifting out of sync with history. This was the original motivation for the redesign.

3. **Collection must be loaded:** Any code path accessing `CurrentWorkflowState` must ensure `WorkflowStateHistories` is loaded. Audit all `GetCaseAsync` and `GetCasesAsync` calls to confirm they `$expand` the collection. The `GetCaseAsync` already expands `WorkflowStateHistories` (see `CaseHttpService.cs` lines 101–108).

4. **Ordering tiebreaker:** Always use `CreatedDate DESC, Id DESC` to handle entries with identical timestamps (see existing repo memory about this).

5. **Rollback in state machine:** Currently, `SaveAndNotifyAsync` rolls back by resetting `_lineOfDutyCase.WorkflowState = previousState`. After the redesign, no rollback is needed — if the POST fails, no history entry was saved, so `CurrentWorkflowState` still returns the previous state. This is actually *simpler* and safer.

6. **`CaseTransitionRequest.NewWorkflowState`:** After removal, the server derives the new state from the last history entry in the request. The server should validate that the entries are consistent (e.g., the last entry's state matches expectations).
