# LineOfDutyStateMachine Recommendations

## 1. Board States Missing Entry Handlers for Return Trigger

**Severity: Critical — BUG** | **Status: Open**

Board states (Steps 8–11) are missing `OnEntryFromAsync` registrations for the
`_returnTrigger`. This means return transitions into these states **silently
skip persistence** — no history entries are created, the case's `WorkflowState`
is not saved, and `_lastTransitionResult` is never set.

### Missing Return Entry Handlers

Steps 2–7 all register `.OnEntryFromAsync(_returnTrigger, OnReturnEntryAsync)`,
but Steps 8–11 do not despite having `.PermitDynamicIf(_returnTrigger, ...)`:

```csharp
// Step 8 — has PermitDynamicIf but NO OnEntryFromAsync for _returnTrigger
_sm.Configure(WorkflowState.BoardMedicalTechnicianReview)
    .OnEntryFromAsync(
        _caseTriggers[
            LineOfDutyTrigger
                .ForwardToBoardTechnicianReview],
        OnBoardMedicalTechnicianReviewEntryAsync)
    // ⚠️ Missing: .OnEntryFromAsync(_returnTrigger, OnReturnEntryAsync)
    .PermitDynamicIf(_returnTrigger, destination => destination, CanReturnAsync)
```

Same gap exists for Steps 9, 10, and 11.

### Lateral Entry Handlers — Not Affected

Lateral board-to-board transitions reuse the same forward triggers (e.g.,
`ForwardToBoardLegalReview`), so the existing `OnEntryFromAsync` registrations
**do fire correctly** for lateral transitions. Only the `_returnTrigger` path is
broken.

**Recommendation:** Add `.OnEntryFromAsync(_returnTrigger, OnReturnEntryAsync)`
to all four board states.

---

## 2. Implement Guard Methods

**Severity: Critical** | **Status: Open**

All 14 guard methods unconditionally return `true`. This means any transition is
always allowed regardless of data completeness.

**Recommendation:** Implement real business rules. Examples:

- `CanForwardToMedicalTechnicianAsync` — Validate Items 1–8 populated (name,
  rank, SSN, unit, incident type, date)
- `CanForwardToUnitCommanderReviewAsync` — Validate medical assessment fields
  complete (EPTS determination, substance involvement)
- `CanCompleteAsync` — Validate all board reviewers have signed off
- `CanCancelAsync` — Check user has authority (role-based guard)
- `CanReturnAsync` — Validate the destination state is a valid return target
  from the current state

Consider making guards `async` and accepting a `LineOfDutyCase` parameter so
they can query the data service for completeness checking.

---

## 3. Add Transactional Atomicity to `SaveAndNotifyAsync`

**Severity: High** | **Status: Open**

`SaveAndNotifyAsync` performs multiple sequential API calls — one
`SaveCaseAsync` followed by N individual `AddHistoryEntryAsync` calls. If
`SaveCaseAsync` succeeds but a subsequent `AddHistoryEntryAsync` fails, the
database is left in an inconsistent state: the case's `WorkflowState` is updated
but the corresponding history entries are missing or incomplete.

```csharp
// Current: N+1 API calls with no transactional guarantee
saved = await _dataService.SaveCaseAsync(_lineOfDutyCase);

foreach (var entry in entriesToSave)
{
    entry.LineOfDutyCaseId = saved.Id;
    savedEntries.Add(
        await _dataService
            .AddHistoryEntryAsync(entry));
    // ⚠️ Any of these can fail
}
```

The try/catch only reverts the in-memory state — it cannot undo a
partially-committed database write.

**Recommendation:** Either:

1. Add a batch `AddHistoryEntriesAsync(IEnumerable<WorkflowStateHistory>)`
   endpoint to persist all entries in a single server-side transaction, or
2. Wrap the case save + history creation in a single API endpoint (e.g.,
   `TransitionCaseAsync`) that performs all writes atomically on the server.

This also addresses the **N+1 API call performance concern** — return
transitions can create up to N+1 history entries, each requiring a separate HTTP
round-trip.

---

## 4. Eliminate Repetitive Entry Handlers

**Severity: Medium** | **Status: Partially Addressed**

The shared `SaveAndNotifyAsync` helper now encapsulates all entry handler logic
— state update, history recording, persistence with try/catch rollback, and
result capture via `_lastTransitionResult`. Each forward entry handler is now a
trivial 1-line wrapper:

```csharp
private async Task OnXxxEntryAsync(LineOfDutyCase lineOfDutyCase)
{
    await SaveAndNotifyAsync(lineOfDutyCase, WorkflowState.Xxx);
}
```

A shared `OnReturnEntryAsync(WorkflowState)` handler is registered via
`.OnEntryFromAsync(_returnTrigger, OnReturnEntryAsync)` for states 2–7.

**Remaining opportunity:** The 11 forward entry handler methods are still
individually defined even though they are trivial 1-line wrappers. Further
consolidation (e.g., a single generic handler via a
`Dictionary<LineOfDutyTrigger, WorkflowState>` mapping) could eliminate them
entirely, but the impact is smaller now (~50 lines vs. the original ~300 lines).

---

## 5. Consolidate Exit Handlers

**Severity: Low** | **Status: Open**

All 12 exit handlers return `Task.CompletedTask` with no logic. They exist
solely as placeholders.

**Recommendation:** Remove them from the state configuration until actually
needed. Stateless doesn't require exit handlers. When a specific step needs exit
logic, add only that handler. This removes ~120 lines of dead code.

---

## 6. Remove `Async` Suffix from Synchronous Guards

**Severity: Low** | **Status: Open**

Methods like `CanStartLodAsync()`, `CanCancelAsync()`, `CanReturnAsync()` are
synchronous (`bool` return) but named with `Async`. The XML doc even
acknowledges this: _"Despite the Async suffix (retained for naming
consistency)..."_.

**Recommendation:** Drop the `Async` suffix now while the codebase is still
maturing. If guards become truly async later, rename them then. This avoids
confusing callers about concurrency expectations.

---

## 7. Clean Up Unused Triggers and Orphaned State

**Severity: Low** | **Status: Open**

The `LineOfDutyTrigger` enum has 4 values not used anywhere in the state machine
configuration:

- `ForwardToMemberInformationEntry` — never wired
- `ForwardToApprovingAuthorityReview` — appears to be a duplicate of
  `ForwardToAppointingAuthorityReview`
- `Close` — no transition defined
- `Reopen` — no transition defined

`WorkflowState.Closed = 13` is also defined but never configured.

**Recommendation:** Either implement `Close`/`Reopen` transitions (for
post-completion case management) or remove the unused values to keep the enum
aligned with reality.

---

## Priority Summary

| Priority | Recommendation | Impact | Status |
| --- | --- | --- | --- |
| **P0** | 1. Missing board return handlers | Skips persistence (BUG) | Open |
| **P0** | 2. Implement guards | No validation = invalid transitions | Open |
| **P1** | 3. Add transactional atomicity | Partial writes + N+1 calls | Open |
| **P2** | 4. Consolidate entry handlers | ~50 lines of wrappers | Partial |
| **P2** | 5. Remove placeholder exits | ~120 lines of dead code | Open |
| **P3** | 6. Remove `Async` suffix from sync guards | Naming accuracy | Open |
| **P3** | 7. Clean up unused triggers/states | Enum hygiene | Open |
