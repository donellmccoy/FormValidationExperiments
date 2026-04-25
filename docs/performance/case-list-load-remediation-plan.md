# Case List / Dashboard Load Remediation Plan

## Context

Trace of an initial page load showed 5 parallel OData requests against `/odata/Cases/ByCurrentState` and `/odata/Cases/Default.Bookmarked()` taking **2.7–3.3 seconds end-to-end**, despite SQL execution times of **8–75 ms**. The dominant cost is over-fetching: every query materializes all ~130 columns of `Cases`, even for `$top=0&$count=true` and `$top=1` calls, and the bookmark/state subquery runs per row without supporting infrastructure.

This plan groups fixes by ROI and risk so they can be merged incrementally.

---

## Phase 1 — Stop over-fetching columns (highest ROI, low risk)

**Problem.** `ByCurrentState` and `Bookmarked()` callers do not pass `$select`. Generated SQL projects all 130+ columns through both the inner `TOP(@n)` and the outer `TOP(@m)` subquery, even when only a count is needed.

**Tasks**

1. Define a slim `LineOfDutyCaseListItem` projection (or reuse `LineOfDutyCaseListItemDto` if it exists) with only the fields the case-list/bookmarks/dashboard grids render:
   - `Id`, `CaseId`, `MemberName`, `MemberRank`, `Component`, `IncidentType`, `InitiationDate`, `FinalFinding`, `IsCheckedOut`, `CheckedOutByName`, `ModifiedDate`, plus the derived "current workflow state" (see Phase 3).
2. Update every grid call site to pass `$select` for the columns above. Audit:
   - [CaseList.razor.cs](ECTSystem.Web/Pages/CaseList.razor.cs)
   - [MyBookmarks.razor.cs](ECTSystem.Web/Pages/MyBookmarks.razor.cs)
   - [Dashboard.razor.cs](ECTSystem.Web/Pages/Dashboard.razor.cs)
   - Any usage of `CaseService.GetCasesByCurrentStateAsync(...)` and `BookmarkService.GetBookmarkedCasesByCurrentStateAsync(...)` that omits `select`.
3. For `$top=0&$count=true` and `$top=1` "is there any?" calls, ensure `$select=Id` is passed so the row payload collapses to one column.
4. Confirm `[EnableQuery]` on the Cases controller actions allows `Select` in `AllowedQueryOptions` (no change if it's already `All`).

**Expected impact.** Response times drop from ~2.7s to a few hundred ms; payload size drops by ~95%.

---

## Phase 2 — Coalesce duplicate widget calls (medium ROI, low risk)

**Problem.** A single page load fires:
- 2× `Default.Bookmarked()` (`$top=0&$count=true` and `$top=5&$orderby=Id desc`)
- 3× `ByCurrentState` (count-by-filter, count-with-search, top 5)

These are issued independently by separate widgets/components, with no shared cache.

**Tasks**

1. Add a server endpoint `GET /odata/Cases/Stats` (or a controller action) that returns a single DTO:
   ```csharp
   public sealed class CaseListStatsDto
   {
       public int TotalActive { get; init; }
       public int TotalBookmarked { get; init; }
       public IReadOnlyList<CaseListItemDto> RecentActive { get; init; }
       public IReadOnlyList<CaseListItemDto> RecentBookmarked { get; init; }
   }
   ```
   so dashboard widgets need a single round trip.
2. Have `BookmarkCountService` populate from this single response (or expose a `RefreshAsync()` that the dashboard calls once on init), and have widgets subscribe rather than re-fetch.
3. Where consolidation isn't worth it, deduplicate at the service layer by caching the in-flight `Task<T>` for identical query keys for the duration of the request (simple `Dictionary<string, Task<T>>` keyed on URL).

**Expected impact.** 5 requests → 1–2 requests on initial load.

---

## Phase 3 — Remove the per-row workflow-state subquery (medium ROI, medium risk)

**Problem.** Every list query contains:
```sql
COALESCE((SELECT TOP(1) WorkflowState FROM WorkflowStateHistory
          WHERE LineOfDutyCaseId = c.Id ORDER BY Id DESC), 0)
```
…re-evaluated per row, used both in `WHERE` and (effectively) projection.

**Tasks**

1. **Denormalize**: add `CurrentWorkflowState` (and optionally `CurrentWorkflowStateChangedAt`) to `Cases`.
   - EF Core migration: nullable column with backfill script populating from latest `WorkflowStateHistory` row per case.
   - Maintain in `WorkflowHistoryService` / state-machine commit path: any insert into `WorkflowStateHistory` updates `Cases.CurrentWorkflowState` in the same transaction.
2. Replace controller filter expressions to compare `c.CurrentWorkflowState` directly. Filter and sort become a single indexed column compare.
3. Add index `IX_Cases_CurrentWorkflowState_IsDeleted_Id`.
4. **Fallback (if denormalization is rejected)**: add covering index `IX_WorkflowStateHistory_LineOfDutyCaseId_Id_DESC INCLUDE (WorkflowState)` so the `TOP(1)` subquery is a single seek.

**Expected impact.** Removes the only non-trivial part of the SQL plan; counts and filtered queries become sub-10 ms regardless of dataset size.

---

## Phase 4 — Cleanup / observability (low ROI, trivial)

1. **CORS preflight cache.** API CORS policy: set `Access-Control-Max-Age` to 600s so the browser stops issuing `OPTIONS` for every call.
2. **Polly noise.** Lower `Polly` log category to `Warning` in `appsettings.Development.json` so successful first-attempt 200s stop logging at `Information`.
3. **Useful "Querying all LOD cases" log.** Either downgrade to `Debug` or include the OData filter / top / skip on the message; the current message is identical for every request and adds no diagnostic value.
4. **Generic OPTIONS noise.** Optionally suppress `RequestLoggingMiddleware` for `OPTIONS` requests, or log them at `Debug`.

---

## Suggested merge order

1. Phase 1 (`$select` everywhere) — single PR, immediate user-visible win.
2. Phase 4 (logging + CORS preflight) — single small PR, no behavior change.
3. Phase 2 (`Stats` endpoint + dedupe) — one PR per widget consolidation.
4. Phase 3 (denormalize `CurrentWorkflowState`) — single PR with migration, backfill, and index. Treat as the long-term fix once Phase 1 has reduced urgency.

## Validation

- Re-run the same scenario after each phase and capture:
  - Total wall-clock for the 5 requests on initial load.
  - Bytes transferred per request (browser DevTools).
  - SQL captured by EF logging — confirm column list shrinks (Phase 1) and subquery disappears (Phase 3).
- Add a smoke integration test that asserts `ByCurrentState` with `$select=Id,CaseId,MemberName` returns only those properties populated.
