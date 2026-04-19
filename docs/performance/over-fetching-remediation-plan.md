# Over-Fetching Remediation Plan

This plan implements and verifies the recommendations from the over-fetching evaluation of the ECTSystem solution. It is sequenced from low-risk, isolated edits to higher-risk, cross-cutting changes.

## Phase 1 — Quick wins (low risk, isolated edits)

### Task 1.1 — Project Dashboard list cards with `$select`
**File:** [ECTSystem.Web/Pages/Dashboard.razor.cs](../../ECTSystem.Web/Pages/Dashboard.razor.cs)

**Change:**
- Define `private const string DashboardCardSelect = "Id,CaseId,MemberName,Unit,IncidentDate,ProcessType";` (matches the columns actually rendered in [Dashboard.razor](../../ECTSystem.Web/Pages/Dashboard.razor) lines ~221‑247).
- Add `select: DashboardCardSelect` and `expand: "WorkflowStateHistories($select=Id,WorkflowState)"` to `actionRequiredTask` and `bookmarksTask`. The expand is required because the status column calls `lodCase.GetCurrentWorkflowState()` which derives from `WorkflowStateHistories`.
- Leave `myActiveCasesTask` / `completedCasesTask` alone (already `top:1`).

**Verify:**
- Build clean.
- Run app, open `/dashboard`. Network tab → confirm requests carry `$select=Id,CaseId,...` and response payload size for the action-required and bookmarks calls drops noticeably.
- Visually confirm both cards render CaseId, MemberName, Unit, status badge, IncidentDate, and ProcessType.

### Task 1.2 — Fix "My Active Cases" filter (correctness + injection)
**File:** [ECTSystem.Web/Pages/Dashboard.razor.cs](../../ECTSystem.Web/Pages/Dashboard.razor.cs)

**Verified preconditions:**
- `CreatedBy` is a `string` on [AuditableEntity.cs](../../ECTSystem.Shared/Models/AuditableEntity.cs) line 7, inherited by `LineOfDutyCase`. It stores the server-assigned user id (not a username).
- `CurrentUserService.GetUserIdAsync()` ([CurrentUserService.cs](../../ECTSystem.Web/Services/CurrentUserService.cs) L25) returns that same id (or `null` if unauthenticated). Already used in [EditCase.razor.cs](../../ECTSystem.Web/Pages/EditCase.razor.cs) L348.

**Change:**
- Add `[Inject] private CurrentUserService CurrentUserService { get; set; }` to Dashboard.razor.cs.
- Resolve `var userId = await CurrentUserService.GetUserIdAsync();` at the top of `LoadDashboardDataAsync`.
- **Guard:** if `userId is null`, set `myActiveCasesCount = 0` and skip the request entirely (don't issue a malformed filter).
- **Mandatory** quote-escape: `var safeUserId = userId.Replace("'", "''");` (defense in depth — even though it is presently always a GUID).
- Replace `filter: $"contains(MemberName, '{userName}')"` with `filter: $"CreatedBy eq '{safeUserId}'"`.
- Audit any other `contains(...,'{userName}')` interpolations in this file — apply the same escape if kept, or drop them.

**Verify:**
- Sign in as a known user with active cases; "My Active Cases" count matches DB query: `SELECT COUNT(*) FROM Cases WHERE CreatedBy = @userId AND CurrentState NOT IN (Completed, Closed, Cancelled)`.
- Sign in as a user with no cases → count is 0 (previously may have been non-zero due to substring collision).
- Sign in as a user whose id contains a `'` (or simulate via test claim) → request still succeeds with no OData parse error.

### Task 1.3 — Cache user display names in EditCase.Documents
**File:** [ECTSystem.Web/Pages/EditCase.Documents.razor.cs](../../ECTSystem.Web/Pages/EditCase.Documents.razor.cs)

**Change:**
- Before calling `UserService.GetDisplayNamesAsync`, filter `userIds` to remove any already in `_userDisplayNames`.
- Only call the service if the filtered list is non-empty.

**Verify:**
- Open a case with documents from multiple uploaders; page through the documents grid. Network tab → first page issues a `GetDisplayNamesAsync` call; subsequent pages with overlapping users issue zero or only-delta calls.

---

## Phase 2 — Trim default `FullExpand` (medium risk; touches many code paths)

### Task 2.1 — Audit consumers of `Notifications` and `WorkflowStateHistories`
**Goal:** Confirm what currently depends on these collections being pre-loaded.

**Steps:**
- Grep for `_lineOfDutyCase.Notifications`, `_lineOfDutyCase.WorkflowStateHistories`, and `lodCase.Notifications` / `lodCase.WorkflowStateHistories` across `ECTSystem.Web`.
- For each consumer, decide: (a) lazy-load on tab activation, (b) replace with a count-only OData call, or (c) keep eager.

### Task 2.2 — Remove `Notifications` from default expand
**File:** [ECTSystem.Web/Services/CaseService.cs](../../ECTSystem.Web/Services/CaseService.cs)

**Change:**
- New constant: `FullExpand = "Authorities,Appeals($expand=AppellateAuthority),Member,MEDCON,INCAP"` (drop `Notifications`).
- Add `GetNotificationCountAsync(int caseId)` (notifications service) using `$top=0&$count=true`.
- Replace `NotificationCount => _lineOfDutyCase?.Notifications?.Count ?? 0` with a cached field populated alongside `LoadDocumentCountAsync` in `LoadCaseAsync`'s `Task.WhenAll`.
- When the Notifications tab is opened (or panel rendered), lazy-load the actual list.

**Verify:**
- Open a case with N notifications. Sidebar/badge shows N. Notifications tab still renders the list correctly.
- Network tab: the case GET response no longer includes a `Notifications` array; a separate count call fires in parallel.

### Task 2.3 — Remove `WorkflowStateHistories` from default expand
**Files:** [ECTSystem.Web/Services/CaseService.cs](../../ECTSystem.Web/Services/CaseService.cs), [ECTSystem.Web/Pages/EditCase.razor.cs](../../ECTSystem.Web/Pages/EditCase.razor.cs)

**Verified preconditions:**
- `_trackingPreloaded` is set in [EditCase.razor.cs L354‑357](../../ECTSystem.Web/Pages/EditCase.razor.cs) when `WorkflowStateHistories` arrives via `$expand`, and is checked in `LoadTrackingData` (~L564) to skip the round-trip.
- `GetCurrentWorkflowState()` ([LineOfDutyExtensions.cs L42‑49](../../ECTSystem.Shared/Extensions/LineOfDutyExtensions.cs)) requires at minimum `Id` + `WorkflowState` per history row to derive the badge state used on the Edit page header and sidebar.

**Change:**
- Drop `WorkflowStateHistories` from `FullExpand`. Replace with a **minimal** expand on the case GET: `WorkflowStateHistories($select=Id,WorkflowState)` so `GetCurrentWorkflowState()` keeps working without dragging full history rows.
- Remove the `_trackingPreloaded` shortcut. The Tracking grid already lazy-loads via `LoadTrackingData` and a `WorkflowHistoryService`.
- Keep the same minimal `WorkflowStateHistories($select=Id,WorkflowState)` expand on list/grid queries (e.g., `CaseList` already does this).

**Tradeoff (must be accepted):**
- First click on the Tracking tab will now always issue an HTTP round-trip (previously free for cases opened with default expand). This is intentional and matches every other lazy-loaded tab. Verify perceptible latency on a case with >100 history rows is acceptable; if not, consider keeping `_trackingPreloaded` and reading from the same minimal expand.

**Verify:**
- Open a case; status badge / sidebar still render the correct current state.
- Click Tracking tab; the grid loads via `WorkflowHistoryService` (Network tab shows the request).
- Case GET payload size drops significantly for cases with long workflow histories.

---

## Phase 3 — Server-side join for bookmarked-by-state (higher risk)

### Task 3.1 — Add OData function `BookmarkedByCurrentState`
**File:** [ECTSystem.Api/Controllers/CasesController.cs](../../ECTSystem.Api/Controllers/CasesController.cs) (and EDM model registration in `ECTSystem.Api/Extensions`).

**Verified preconditions:**
- Join key is `Bookmark.UserId` ([Bookmark.cs](../../ECTSystem.Shared/Models/Bookmark.cs) L9) ↔ current authenticated user id (same value `CurrentUserService` returns).
- Existing `Default.Bookmarked` function on `Cases` already encapsulates the user-scoped filter — re-use that pattern.

**Change:**
- Register a new function on `Cases` returning `IQueryable<LineOfDutyCase>` accepting `includeStates`, `excludeStates` arrays.
- Implementation: server-side LINQ joining `Bookmarks` (filtered by `UserId == currentUserId`) with `Cases`, then applying the state filter.
- Decorate with `[EnableQuery(MaxTop=100, PageSize=50, MaxExpansionDepth=3, MaxNodeCount=500)]` so client `$select`/`$top`/`$count`/`$filter` still work.

### Task 3.2 — Replace 2-trip client method
**File:** [ECTSystem.Web/Services/BookmarkService.cs](../../ECTSystem.Web/Services/BookmarkService.cs)

**Change:**
- Re-implement `GetBookmarkedCasesByCurrentStateAsync` to call the new function in a single round-trip. Mirror the parameter shape of `CaseService.GetCasesByCurrentStateAsync`.
- Keep the old method's signature so callers don't change.

**Verify:**
- Unit test in `ECTSystem.Tests` that seeds a user with >50 bookmarks across multiple states and asserts:
  - One HTTP request (not two).
  - Correct count + ordering.
  - `$select` is honored.
- Manual: My Bookmarks page filtered by state still works; Network tab shows a single request.

---

## Phase 4 — Optional polish

### Task 4.1 — `Prefer: return=minimal` on PATCH
**File:** `CaseService.SaveCaseAsync`

**Change:**
- Add `Prefer: return=minimal` header. Read updated `RowVersion` from the `ETag` response header instead of the body.
- Server controller must honor `Prefer` and emit `ETag`.

**Verify:**
- Save a case → response body is empty/minimal; `_lineOfDutyCase.RowVersion` is updated from header. Subsequent saves succeed (no 412 Precondition Failed).

### Task 4.2 — Implement real Dashboard charts via `$apply`
**Server:** Ensure `[EnableQuery]` on Cases allows `$apply` (it does by default).

**Client:** Replace mock `casesOverTimeData` / `casesByStatusData` with calls using `$apply=groupby((WorkflowState),aggregate($count as Count))` for status, and a date-bucketed `$apply` for over-time.

**Verify:** Charts render real data; payload size is one row per group, not per case.

---

## Cross-cutting verification

After each phase:

1. **Build:** run task `build` (workspace task).
2. **Unit/integration tests:** `dotnet test ECTSystem.Tests/ECTSystem.Tests.csproj`. Focus on `Controllers/CasesControllerTests`, `Integration/*`, and any `BookmarkService` tests.
3. **Manual smoke (Simple Browser at `https://localhost:7240`):**
   - `/dashboard` — all 4 KPIs populate; cards render; no console errors.
   - `/cases` — list loads, paging + filters + search work.
   - Open a case — all tabs (Assessment, Documents, Tracking, Previous Cases, Notifications) render correctly.
   - `/bookmarks` — bookmarked cases list loads; state filter works.
   - Save a case (PATCH) — no 412 errors; subsequent edits succeed.
4. **Network audit (Browser DevTools → Network, filter to `/odata/`):**
   - For each scenario above, capture (a) request count and (b) total transferred bytes.
   - Compare to a baseline captured before changes.
   - Targets:
     - Dashboard total bytes ↓ at least 50% (Task 1.1).
     - Case open total bytes ↓ noticeably for cases with many notifications/history (Task 2.2 / 2.3).
     - Bookmarked-by-state: 2 requests → 1 request (Task 3.2).
5. **Logs:** API console shows no new warnings/errors; OData logging handler shows expected URL shapes (`$select`, `$top`, `$count`).

---

## Suggested commit / PR sequence

1. **PR #1:** Phase 1 (1.1, 1.2, 1.3) — small, mergeable independently.
2. **PR #2:** Phase 2.2 (Notifications).
3. **PR #3:** Phase 2.3 (WorkflowStateHistories).
4. **PR #4:** Phase 3 (server function + client switch).
5. **PR #5 (optional):** Phase 4 items.

---

## Baseline metrics (to capture before starting Phase 1)

| Scenario | Requests | Total bytes |
|---|---|---|
| `/dashboard` initial load |  |  |
| `/cases` initial load |  |  |
| Open case (small: <5 notifications, <10 history) |  |  |
| Open case (large: >20 notifications, >30 history) |  |  |
| `/bookmarks` filtered by state |  |  |
| Save case (PATCH) response size |  |  |
