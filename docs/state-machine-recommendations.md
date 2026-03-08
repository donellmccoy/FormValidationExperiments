# LineOfDutyStateMachine Recommendations

## 1. Eliminate Repetitive Entry Handlers

**Severity: High** — 11 of 14 entry handlers (Steps 1–11) are identical copy-paste with only the `WorkflowState` and callback name varying:

```csharp
private async Task OnXxxEntryAsync(LineOfDutyCase lineOfDutyCase)
{
    _lineOfDutyCase = lineOfDutyCase;
    _lineOfDutyCase.UpdateWorkflowState(WorkflowState.Xxx);
    _lineOfDutyCase.AddWorkflowStateHistory(WorkflowState.Xxx);
    var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);
    await OnXxxEntered?.Invoke(saved);
}
```

**Recommendation:** Extract a single generic handler and resolve the callback by state:

```csharp
private async Task OnStateEntryAsync(WorkflowState state, LineOfDutyCase lineOfDutyCase)
{
    _lineOfDutyCase = lineOfDutyCase;
    _lineOfDutyCase.UpdateWorkflowState(state);
    _lineOfDutyCase.AddWorkflowStateHistory(state);
    var saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

    if (_entryCallbacks.TryGetValue(state, out var callback))
        await callback?.Invoke(saved);
}
```

Replace the 13 individual `Func<LineOfDutyCase, Task>` callback properties with a `Dictionary<WorkflowState, Func<LineOfDutyCase, Task>>` populated via a single `OnStateEntered(WorkflowState, Func<...>)` registration method. This eliminates ~300 lines of boilerplate.

---

## 2. Implement Guard Methods

**Severity: Critical** — All 19 guard methods unconditionally return `true`. This means any transition is always allowed regardless of data completeness.

**Recommendation:** Implement real business rules. Examples:

- `CanForwardToMedicalTechnicianAsync` — Validate Items 1–8 populated (name, rank, SSN, unit, incident type, date)
- `CanForwardToUnitCommanderReviewAsync` — Validate medical assessment fields complete (EPTS determination, substance involvement)
- `CanCompleteAsync` — Validate all board reviewers have signed off
- `CanCancelAsync` — Check user has authority (role-based guard)
- `CanReturnAsync` — Validate the destination state is a valid return target from the current state

Consider making guards `async` and accepting a `LineOfDutyCase` parameter so they can query the data service for completeness checking.

---

## 3. Remove `Async` Suffix from Synchronous Guards

**Severity: Low** — Methods like `CanStartLodAsync()`, `CanCancelAsync()`, `CanReturnAsync()` are synchronous (`bool` return) but named with `Async`. The XML doc even acknowledges this: *"Despite the Async suffix (retained for naming consistency)..."*.

**Recommendation:** Drop the `Async` suffix now while the codebase is still maturing. If guards become truly async later, rename them then. This avoids confusing callers about concurrency expectations.

---

## 4. Consolidate Exit Handlers

**Severity: Low** — All 12 exit handlers return `Task.CompletedTask` with no logic. They exist solely as placeholders.

**Recommendation:** Remove them from the state configuration until actually needed. Stateless doesn't require exit handlers. When a specific step needs exit logic, add only that handler. This removes ~120 lines of dead code.

---

## 5. Replace 13 Callback Properties with Event/Dictionary Pattern

**Severity: Medium** — The 13 `Func<LineOfDutyCase, Task>` properties create a wide surface area that the consumer (`EditCase`) must wire up individually.

**Recommendation:** Use either:
- **Option A (Dictionary):** A single `RegisterCallback(WorkflowState, Func<LineOfDutyCase, Task>)` method — simpler, pairs with Recommendation 1.
- **Option B (Single event):** A single `event Func<WorkflowState, LineOfDutyCase, Task> OnStateEntered` that fires for every transition. The consumer can switch on the state if step-specific behavior is needed. Most consumer callbacks currently do the same thing (update viewmodel, update sidebar, notify), so a single handler is likely sufficient.

---

## 6. Clean Up Unused Triggers and Orphaned State

**Severity: Low** — The `LineOfDutyTrigger` enum has 4 values not used anywhere in the state machine configuration:
- `ForwardToMemberInformationEntry` — never wired
- `ForwardToApprovingAuthorityReview` — appears to be a duplicate of `ForwardToAppointingAuthorityReview`
- `Close` — no transition defined
- `Reopen` — no transition defined

`WorkflowState.Closed = 13` is also defined but never configured.

**Recommendation:** Either implement `Close`/`Reopen` transitions (for post-completion case management) or remove the unused values to keep the enum aligned with reality.

---

## 7. Return `StateMachineResult` from `FireAsync`

**Severity: Medium** — `FireAsync` returns `Task` (void), pushing callers to use callbacks for results. `SaveCaseAsync` returns `StateMachineResult` with structured success/failure, but `FireAsync` doesn't.

**Recommendation:** Have `FireAsync` return `StateMachineResult` with the saved case and tab index. This lets callers handle the result inline:

```csharp
var result = await _stateMachine.FireAsync(lineOfDutyCase, trigger);
if (result.Success)
{
    _lineOfDutyCase = result.Case;
    _selectedTabIndex = result.TabIndex;
}
```

This simplifies the consumer and may eliminate the need for per-state callbacks entirely.

---

## 8. Add Error Handling in Entry Handlers

**Severity: Medium** — Entry handlers call `_dataService.SaveCaseAsync()` but have no try/catch. If the API save fails mid-transition, the state machine has already advanced but persistence hasn't occurred, leaving the in-memory state diverged from the database.

**Recommendation:** Wrap the save call in error handling and consider what the correct recovery is — either roll back the state machine state or surface the error to the UI. `SaveCaseAsync` already demonstrates this pattern with try/catch returning `StateMachineResult.Fail()`.

---

## 9. Move Tab Mapping Responsibility Out of the State Machine

**Severity: Low** — `WorkflowTabMap`, `GetTabIndexForState()`, and `IsTabDisabled()` are UI concerns (which RadzenTabs tab to select/disable). They don't relate to state machine transitions.

**Recommendation:** Move these to a standalone static helper (e.g., `WorkflowTabHelper`) or into the `EditCase` code-behind. The state machine should focus strictly on workflow transitions and persistence.

---

## 10. Consider Making `_lineOfDutyCase` Immutable After Construction

**Severity: Low** — `_lineOfDutyCase` is mutated in-place across transitions (via `UpdateWorkflowState`, `AddWorkflowStateHistory`) and completely replaced in entry handlers (`_lineOfDutyCase = lineOfDutyCase`). This makes it hard to reason about the case reference's lifecycle.

**Recommendation:** Treat the case passed to `FireAsync` as the single source of truth. The entry handler already receives it as a parameter — avoid also storing it as mutable field state that can be replaced by any transition.

---

## Priority Summary

| Priority | Recommendation | Impact |
|----------|---------------|--------|
| **P0** | 2. Implement guard methods | No validation = invalid transitions allowed |
| **P1** | 1. Consolidate entry handlers | Eliminates ~300 lines of identical boilerplate |
| **P1** | 5. Replace 13 callback properties | Simplifies consumer wiring |
| **P1** | 8. Add error handling in entry handlers | Prevents state/DB divergence |
| **P2** | 7. Return `StateMachineResult` from `FireAsync` | Cleaner consumer API |
| **P2** | 4. Remove placeholder exit handlers | Removes ~120 lines of dead code |
| **P3** | 3. Remove `Async` suffix from sync guards | Naming accuracy |
| **P3** | 6. Clean up unused triggers/states | Enum hygiene |
| **P3** | 9. Move tab mapping out | Separation of concerns |
| **P3** | 10. Immutable case reference | Easier state reasoning |
