# CaseList & MyBookmarks — Improvement Suggestions

Both pages share roughly 80% of their code, so most wins compound.

## High impact

### 1. Extract a shared base class — `CaseGridPageBase` — ⏳ Pending
`CaseList` and `MyBookmarks` duplicate: `LoadData`, `OnSearchInput`, `OnCaseClick`, `OnCellContextMenu`, `ShowContextMenuAsync`, `Dispose`, all CTS/grid/state fields, `_workflowStateFilters` / `_incidentTypeFilters` / `_processTypeFilters`, the entire ~50-column search-columns block, and the enum-to-display tooltip. Move it into `Pages/Shared/CaseGridPageBase.cs` and override only `BuildFilter`, `OnRemoveBookmark` vs `ToggleBookmark`, and the navigation `from=` token. This collapses ~700 lines into ~150 + two thin subclasses, eliminates drift (e.g. the 700ms vs 900ms debounce inconsistency, `searchText` vs `_searchText` naming, `TooltipService` vs `_tooltipService`).

### 2. Latent NRE in `CaseList.ToggleBookmark` — ✅ Done
[CaseList.razor.cs](../ECTSystem.Web/Pages/CaseList.razor.cs#L353) does `lodCase.BookmarkId!.Value` with no null guard — `MyBookmarks.OnRemoveBookmark` correctly checks. If the projection ever drops `UserId` again (the bug recently fixed), this throws NRE instead of a friendly notification. Add the same guard.

### 3. Stop the redundant `IsBookmarkedAsync` round-trip on right-click — ✅ Done
[CaseList.razor.cs](../ECTSystem.Web/Pages/CaseList.razor.cs#L656) calls `BookmarkService.IsBookmarkedAsync(lodCase.Id)` to decide the menu label — but `lodCase.IsBookmarked` is already populated from the grid load. Use the row data; remove the extra HTTP call (one per right-click).

### 4. Trim `BuildFilter` — ~50 OR clauses is a server killer — ⏳ Pending
The free-text search produces a single OData expression with ~50 `contains()` clauses ORed together against unindexed text columns. Options:
- Restrict to a tier-1 set (`CaseId, MemberName, ServiceNumber, Unit, MemberRank`) by default and offer "deep search" as an opt-in toggle.
- Move to a server-side `/cases/search?q=…` endpoint backed by a full-text index (PostgreSQL `tsvector` or SQL Server FTS) and return matched IDs.
- Promote the columns array to `private static readonly string[] SearchColumns = […]` so it isn't reallocated on every keystroke.

## Medium impact

### 5. Fire-and-forget context menu swallows exceptions — ✅ Done
`_ = ShowContextMenuAsync(args)` discards the task. If `BookmarkService` or `JSRuntime` throws, nothing logs it. Wrap with a helper:
```csharp
private void OnCellContextMenu(DataGridCellMouseEventArgs<CaseListItemViewModel> args)
    => _ = SafeRun(() => ShowContextMenuAsync(args));

private async Task SafeRun(Func<Task> work)
{
    try { await work(); }
    catch (Exception ex) { Logger.LogError(ex, "Context menu action failed"); }
}
```

### 6. URL-sync filters and search — ✅ Done
Both pages now expose `?q=`, `?state=`, `?incident=`, and `?process=` via `[SupplyParameterFromQuery]`. `HydrateFromQuery` seeds the filter fields on first parameter set and queues a grid reload on subsequent URL changes (browser back/forward, external navigation). `SyncUrl` writes the current filter state back to the query string with `replace: true` so history isn't polluted on each keystroke, and is invoked from the debounced search handler and every filter `Change` callback. Empty values are omitted so shared links stay clean.

### 7. Cancel debounced searches on dispose — ✅ Done
`Dispose` cancels `_searchCts`, but the `Task.Delay` continuation that survives cancellation calls `_grid.FirstPage(true)` on a disposed component if cancellation races. Move the `_grid.FirstPage` call inside the `try` block after `await Task.Delay`, then check `token.IsCancellationRequested` before calling.

### 8. Make `LineOfDutyCaseMapper.ToCaseListItem` projection-friendly — ⏳ Pending
The recently fixed bug (UserId stripped → bookmark predicate silently fails) is structural: the mapper takes a denormalized graph and re-filters client-side. Either:
- Push the per-user lookup into a stronger DTO contract (a single `Bookmark? CurrentUserBookmark` projected server-side), so `BookmarkId = source.CurrentUserBookmark?.Id` doesn't depend on a hidden column being present.
- Or have the mapper throw/log when `Bookmarks.Any() && all UserId == null` to surface the projection mistake immediately.

#### How would `CurrentUserBookmark` actually be computed?

With OData + EF on a one-to-many `Case → Bookmarks` relationship, you can't reshape the wire payload into a true single-valued `CurrentUserBookmark` navigation property — `$expand` always returns a JSON array. So "stronger DTO contract" really means one of these three concrete shapes:

##### A1. Keep the collection on the wire, expose a computed property on the client model

The server projection stays as it is today ([CaseList.razor.cs#L229](../ECTSystem.Web/Pages/CaseList.razor.cs#L229)):

```csharp
Bookmarks($filter=UserId eq '{_currentUserId}';$select=Id,UserId;$top=1)
```

The `$filter` already guarantees at most one bookmark, for the current user. On the model, add:

```csharp
// LineOfDutyCase.cs
[NotMapped, JsonIgnore]
public Bookmark? CurrentUserBookmark => Bookmarks?.FirstOrDefault();
```

The mapper becomes:

```csharp
IsBookmarked = source.CurrentUserBookmark is not null,
BookmarkId   = source.CurrentUserBookmark?.Id
```

The fragility goes away because the mapper no longer references `b.UserId` — dropping `UserId` from `$select` would no longer break the predicate (only the `$filter` matters, and that runs server-side). The contract you're enforcing: "by convention, `Bookmarks` on a list query is pre-filtered to the current user, so `.FirstOrDefault()` is the user's bookmark." Smallest change; silent-failure mode is gone. The `_currentUserId` is still embedded in the expand string (a separate smell), but the regression class is closed.

##### A2. Server-projected DTO with a scalar bookmark id

Add a real list endpoint (not raw OData over the entity) that returns a `CaseListItemDto` directly, with `Guid? BookmarkId` projected server-side:

```csharp
context.Cases.Select(c => new CaseListItemDto {
    /* ... */
    BookmarkId = c.Bookmarks
        .Where(b => b.UserId == currentUserId)
        .Select(b => (Guid?)b.Id)
        .FirstOrDefault()
})
```

Wire format becomes a single nullable scalar — no collection, no `UserId` exposure, no client-side filter. The "right" shape, but requires a new controller action and giving up some OData query niceties on this endpoint (or layering OData on top of the projected `IQueryable<CaseListItemDto>`).

##### A3. OData function returning the list view

`GET /odata/Cases/CaseList(userId=...)` returns a typed `IQueryable<CaseListItemDto>` and OData still gives you `$filter`, `$orderby`, `$top`, `$skip`, `$count` over the projected DTO. Same wire benefit as A2; preserves the grid's server-side paging/filter/sort behavior.

##### Recommendation

**A1 today** (5-line change, kills the silent-failure regression class). **A3 if/when item #1 lands** and a shared `CaseGridPageBase` makes it natural to swap the data source. A2 is fine but loses OData ergonomics on this endpoint.

### 9. Selection logic auto-selects on every page change — ✅ Done
`if (firstItem != null && !_selectedCases.Any(c => c.Id == firstItem.Id)) _selectedCases = [firstItem];` ran on every `LoadData`, so changing filter/page silently re-selected row 1 even if the user had clicked nothing. Auto-select now only fires when `!_initialLoadComplete`; subsequent loads preserve the user's selection and prune any rows no longer present in the current page.

## Lower impact / polish

### 10. Naming consistency — ✅ Done (CaseList)
- `CaseList` uses `searchText` (no underscore); `MyBookmarks` uses `_searchText`. Pick one.
- `CaseList` uses `_tooltipService` (camelCase + underscore on a `[Inject]` property); should be `TooltipService` to match every other injected property.

### 11. Two near-identical tooltip strings — ✅ Done (per-class `const SearchTooltipText`; shared base class deferred to #1)
Move the search-fields tooltip text to a `const string SearchTooltipText = "…";` in the shared base.

### 12. Bookmark badge updates can desync — ✅ Done (CaseList add path now rolls back local state on failure; MyBookmarks delete path already correct)
`BookmarkCountService.Increment()` / `Decrement()` is called optimistically before the OData round-trip completes in `MyBookmarks` (after `await DeleteBookmarkAsync`, ok) but in `CaseList.ToggleBookmark` it runs after a successful add — fine. However, on failure paths neither rolls back a stale local mutation. Wrap state mutation + count update in a try/catch that reverts on exception.

### 13. Grid reload after bookmark add in `CaseList` — ✅ Done
Adding a bookmark mutates `lodCase.IsBookmarked` directly but never refreshes the row's grid binding. Confirm `CaseListItemViewModel` implements `INotifyPropertyChanged` (it inherits `TrackableModel` per the structure) — if not, the icon won't update without `StateHasChanged()`.

Verified: `TrackableModel` is a snapshot/dirty-tracking base and does **not** implement `INotifyPropertyChanged`. Added `StateHasChanged()` after each `IsBookmarked`/`BookmarkId` mutation in `ToggleBookmark` (remove success, add success, add-failure rollback). Critical for the add path because of the trailing `await Task.Delay(800)` for the flash animation.

### 14. `Dispose` should be `IAsyncDisposable` or call `Cancel(throwOnFirstException: false)` — ✅ Done
Calling `Cancel()` on a CTS that already triggered linked work can synchronously surface exceptions. Safer:
```csharp
public void Dispose()
{
    _loadCts.Dispose();
    _searchCts.Dispose();
    GC.SuppressFinalize(this);
}
```
(Cancellation already happens in the next request; explicit `.Cancel()` here is unnecessary if the tokens are no longer observed.)

### 15. `MyBookmarks` filters all `WorkflowState` values — ⏳ Pending
The dropdown shows every workflow state, including `Draft` — which by definition can't be bookmarked by other users. Consider scoping the filter list to states actually present in the user's bookmarks (one-time `GET /cases?$apply=…` on init).

#### What it's about

[MyBookmarks.razor.cs#L239](../ECTSystem.Web/Pages/MyBookmarks.razor.cs#L239) builds the workflow-state filter dropdown from **every** value of the `WorkflowState` enum:

```csharp
private static readonly object[] _workflowStateFilters =
    [.. Enum.GetValues<WorkflowState>().Select(e => (object)new { Value = (WorkflowState?)e, Text = e.ToDisplayString() })];
```

So the dropdown shows `Draft`, `MemberReporting`, `LodInitiation`, … `Completed`, `Cancelled` — the entire state machine.

#### Why that's wrong on this page

`MyBookmarks` only ever shows cases the **current user has bookmarked**. Some of those enum values can never appear there:

- `Draft` — a draft case is by definition the author's working copy; nobody else bookmarks drafts, and even if you bookmark your own draft, most users will never have one in this list.
- `Cancelled` / `Completed` — terminal states that may also be empty for most users.
- Any state the user simply hasn't bookmarked anything in.

Selecting one of those filters returns zero rows and silently makes the user think their bookmarks vanished. It's a discoverability footgun: the dropdown advertises options that produce empty results.

Compare `CaseList`, where showing all states is correct — every state is reachable across the full case set.

#### The fix

One-time query on init that asks the API for the distinct states present in the user's bookmarks, then bind the dropdown to that subset.

A small server endpoint:

```csharp
// CasesController
[HttpGet("BookmarkedStates")]
public IQueryable<WorkflowState> BookmarkedStates()
    => context.Cases
        .Where(c => c.Bookmarks.Any(b => b.UserId == GetAuthenticatedUserId()))
        .Select(c => c.WorkflowStateHistories.OrderByDescending(h => h.Id).First().WorkflowState)
        .Distinct();
```

Then in `MyBookmarks.OnInitializedAsync`:

```csharp
var states = await CaseService.GetBookmarkedStatesAsync();
_workflowStateFilters = states
    .OrderBy(s => s)
    .Select(s => (object)new { Value = (WorkflowState?)s, Text = s.ToDisplayString() })
    .ToArray();
```

Note this means `_workflowStateFilters` can no longer be `static readonly` on this page (it's user-scoped, not type-scoped) — drop those modifiers and make it instance state.

#### Caveats

1. **Staleness.** The list is computed on init; if the user bookmarks a new case in a state that wasn't previously represented, the dropdown won't include it until the next page load. Acceptable for a filter dropdown — refresh on `LoadData` if you want strict consistency, but that adds a round-trip per reload.
2. **Empty-state UX.** If the user has no bookmarks at all, the dropdown should be disabled, not empty.
3. **Stops being a `static readonly` cache.** Slight allocation cost per page mount; negligible.
4. **Ordering.** Enum-default ordering is fine; if you want the workflow-progression order, sort by the enum's underlying int (which already encodes step order in this codebase).

#### Priority

Low. UX polish — no correctness or performance bug. Worth doing alongside item #1 (`CaseGridPageBase`) since "which states populate the filter?" is one of the few real differences between the two pages and is a natural extension hook on the base class.

### 16. Accessibility — ✅ Done
- `ContextMenuItem` has no keyboard activation guarantee — verify `aria-haspopup` / `aria-expanded`.
- The search tooltip uses `Duration = null` (sticky) but isn't `aria-describedby`-linked to the search input.
- Bookmark icon-only buttons need `Title` / `aria-label`.

**Verification:** In both `CaseList.razor` and `MyBookmarks.razor`, search inputs now expose `aria-label` plus `aria-describedby` pointing to a visually-hidden span containing the full `SearchTooltipText` (so AT users get the same field-list help without depending on the hover tooltip). The decorative info `RadzenIcon` is marked `aria-hidden="true"`. Bookmark icon-only `RadzenButton`s now declare a contextual `aria-label` ("Add/Remove bookmark for case {CaseId}"); the toggle button in `CaseList` also exposes `aria-pressed` to reflect bookmark state. (The Radzen context-menu sub-bullet is left as documentation: row context menus open via right-click only and Radzen's `RadzenContextMenu` already handles arrow/Enter/Esc keyboard navigation once opened.)

---

## Recommended order
1. ~~**#2 (NRE fix)** + **#3 (drop redundant `IsBookmarked` call)** — quick wins.~~ ✅ Done
2. **#1 (extract base class)** — prevents future drift.
3. **#4 (trim `BuildFilter`)** — performance.

## Status
- ✅ Applied: #2, #3, #5, #6, #7, #9, #10, #11, #12, #13, #14, #16
- ⏳ Remaining: #1 (base class), #4 (trim BuildFilter), #8 (mapper hardening), #15 (workflow state scoping)
