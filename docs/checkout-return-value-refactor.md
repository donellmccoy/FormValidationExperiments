# Refactor: Propagate `Checkout` return value to callers

> **Status:** ✅ Complete

## Background

The server's `Checkout` OData action returns the updated `LineOfDutyCase` (with a fresh `RowVersion` and the `CheckedOutBy*` / `CheckedOutDate` fields filled in). The client's `CheckOutCaseViaODataAsync` discards this response and returns `bool`, so callers must either reload the grid or navigate to a page that re-fetches the case in order to see fresh state.

This refactor propagates the returned entity to callers so:

- The cached `RowVersion` stays current (currently aspirational — masked by reloads/navigations).
- Callers can update in-memory state without an extra round-trip.

## Scope

- [`CaseService.CheckOutCaseViaODataAsync`](../ECTSystem.Web/Services/CaseService.cs) — implementation
- [`ICaseService`](../ECTSystem.Web/Services/Interfaces/ICaseService.cs) — interface signature
- Call sites:
  - [`CaseList.razor.cs#L775`](../ECTSystem.Web/Pages/CaseList.razor.cs)
  - [`EditCase.razor.cs#L768`](../ECTSystem.Web/Pages/EditCase.razor.cs) (manual checkout from previous-cases grid dialog)
  - [`EditCase.razor.cs#L947`](../ECTSystem.Web/Pages/EditCase.razor.cs) (auto-checkout after create)
  - [`EditCase.razor.cs#L1541`](../ECTSystem.Web/Pages/EditCase.razor.cs) (manual checkout from current case)
  - [`MyBookmarks.razor.cs#L685`](../ECTSystem.Web/Pages/MyBookmarks.razor.cs)

Out of scope:

- Surfacing `ProblemDetails` (title / detail naming the holder) to the user.
- The HTTP `CheckOutCaseAsync` variant (no callers).
- `CheckIn` symmetry — separate follow-up.

## Steps

- [x] **1.** Change `ICaseService.CheckOutCaseViaODataAsync` return type from `Task<bool>` → `Task<LineOfDutyCase?>` (null = failure).
- [x] **2.** Update `CaseService.CheckOutCaseViaODataAsync` to capture `ExecuteAsync<LineOfDutyCase>` result and return `response.SingleOrDefault()`; catches return `null`.
- [x] **3.** Update `CaseList.razor.cs` — replace `var success = ...` with `var updated = ...`; treat `null` as failure (existing toast + reload). On success, navigate (no in-memory merge needed — page navigates away).
- [x] **4.** Update `EditCase.razor.cs#L768` (previous-cases dialog) — same pattern.
- [x] **5.** Update `EditCase.razor.cs#L947` (auto-checkout after create) — assign returned entity's `RowVersion`, `IsCheckedOut`, `CheckedOutBy`, `CheckedOutByName`, `CheckedOutDate` onto `_lineOfDutyCase` so subsequent saves use the fresh `RowVersion`. Do NOT replace the whole object (would wipe loaded navigation collections).
- [x] **6.** Update `EditCase.razor.cs#L1541` (manual checkout on current case) — same as step 3 (navigates away).
- [x] **7.** Update `MyBookmarks.razor.cs#L685` — same as step 3.
- [x] **8.** Build the solution and confirm no errors.

## Risks

- **Navigation property loss** — server returns a sparse entity (no `Member`, `MEDCON`, etc.). Step 5 must merge field-by-field, never assign the whole object.
- **Tests** — grep found no existing checkout test coverage in `ECTSystem.Tests`, so no test updates required.
