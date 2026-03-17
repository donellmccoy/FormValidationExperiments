# Part 5: UI Event Handling & State Changes

## State Architecture

The component uses nested state classes in `EditCase.State.razor.cs`:

| State Class | Purpose | Key Properties |
|---|---|---|
| `PageOperationState` | Global loading/saving indicators | `IsLoading`, `IsSaving`, `IsTransitioning` |
| `BookmarkUiState` | Bookmark toggle + optimistic UI | `IsBookmarked`, `IsToggling` |
| `MemberSearchUiState` | Search input, results, keyboard nav | `SearchText`, `Results`, `SelectedIndex`, `IsLoading` |
| `DocumentUiState` | Upload progress, active operations | `IsUploading`, `UploadProgress`, `IsDeleting` |

## Dirty Tracking via TrackableModel

`LineOfDutyViewModel` extends `TrackableModel`, which provides:
- `TakeSnapshot()` — JSON-serializes the current state
- `IsDirty` — compares current state to snapshot
- `Revert()` — restores from snapshot

This drives the "Apply Changes" / "Revert Changes" button visibility via `HasAnyChanges`.

**Section-level tracking**: Properties are decorated with `[FormSection("SectionName")]` attributes, enabling per-tab dirty detection. `SaveTabFormDataAsync(tabName)` only saves when the active tab's section has changes.

## Event Flow: Tab Changes

```
User clicks tab → _selectedTabIndex updated → OnTabIndexChanged fires
  → If Documents tab: _documentsGrid?.Reload() → LoadDocumentsData (server-side paged)
  → If Form 348 tab: lazy-loads PDF via JS interop (one-time)
  → If Tracking tab: uses preloaded data (first) or grid Reload (subsequent)
```

## Event Flow: Field Changes (Cascading Clears)

Six field-change handlers implement cascading clear logic to prevent stale dependent values:

```
OnIsMilitaryFacilityChanged(bool? → false)
  └→ Clears TreatmentFacilityName

OnWasUnderInfluenceChanged(bool? → false)
  └→ Clears SubstanceType

OnOtherTestsDoneChanged(bool? → false)
  └→ Clears OtherTestDate, OtherTestResults

OnPsychiatricEvalCompletedChanged(bool? → false)
  └→ Clears PsychiatricEvalDate, PsychiatricEvalResults

OnServiceComponentChanged(ServiceComponent)
  └→ Clears IsUSAFA, IsAFROTC (non-ARC members)

OnIncidentTypeChanged(IncidentType)
  └→ Resets medical assessment booleans
```

All handlers are pure client-side — no HTTP calls. They fire `StateHasChanged()` implicitly via Blazor's binding system.

## Event Flow: Workflow Transitions

```
User clicks SplitButton menu item (Forward/Return/Board/Cancel)
  └→ OnForwardClick / OnCompleteClick → FireWorkflowActionAsync(trigger)
       ├─ "revert" → OnRevertChanges() [client-only, no HTTP]
       ├─ "cancel" → NavigationManager.NavigateTo(returnUrl)
       ├─ ReturnTargets → Dialog confirmation → FireReturnAsync
       │    └→ _stateMachine.FireReturnAsync(case, targetState)
       │         └→ SaveAndNotifyAsync: builds N Returned + 1 InProgress entries
       │              └→ TransitionCaseAsync($batch + re-fetch)
       ├─ BoardTargets → FireAsync(boardTrigger)
       │    └→ SaveAndNotifyAsync: builds 1 Completed + 1 InProgress
       │         └→ TransitionCaseAsync($batch + re-fetch)
       └─ Forward trigger → FireAsync(forwardTrigger)
            └→ (same as above)

  └→ ApplyTransitionResult checks StateMachineResult
       ├─ Success: re-map entity→VM, take snapshots, navigate to target tab
       └─ Failure: show error notification
```

## Debounce Patterns

| Context | Delay | Implementation |
|---|---|---|
| Member search | 300ms | `CancellationTokenSource` swap + manual delay |
| Previous cases search | 500ms | `CancellationTokenSource` swap + manual delay |
| Tracking search | 500ms | `CancellationTokenSource` swap + manual delay |

All use the same pattern: cancel the previous `CancellationTokenSource`, create a new one, `await Task.Delay(ms, token)`, then execute. If the token is cancelled during the delay, the search is skipped.

## Optimistic UI: Bookmarks

```
OnBookmarkClick:
  1. Toggle _bookmark.IsBookmarked immediately (optimistic)
  2. StateHasChanged() — UI reflects immediately
  3. await AddBookmarkAsync() or RemoveBookmarkAsync()
  4. On failure: revert _bookmark.IsBookmarked, show error notification
  5. On success: RefreshAsync() to update bookmark count in sidebar/header
```

## Generation-Based Server-Side Paging

The previous cases grid uses an `_previousCasesGeneration` counter to prevent stale responses from overwriting newer data:

```csharp
var generation = ++_previousCasesGeneration;
// ... await HTTP call ...
if (generation != _previousCasesGeneration) return; // stale response, discard
```

This handles the race condition where a new search fires before the previous one completes.
