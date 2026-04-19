# CaseList & MyBookmarks — Improvement Suggestions

Both pages share roughly 80% of their code, so most wins compound.

## High impact

### 1. Extract a shared base class — `CaseGridPageBase`
`CaseList` and `MyBookmarks` duplicate: `LoadData`, `OnSearchInput`, `OnCaseClick`, `OnCellContextMenu`, `ShowContextMenuAsync`, `Dispose`, all CTS/grid/state fields, `_workflowStateFilters` / `_incidentTypeFilters` / `_processTypeFilters`, the entire ~50-column search-columns block, and the enum-to-display tooltip. Move it into `Pages/Shared/CaseGridPageBase.cs` and override only `BuildFilter`, `OnRemoveBookmark` vs `ToggleBookmark`, and the navigation `from=` token. This collapses ~700 lines into ~150 + two thin subclasses, eliminates drift (e.g. the 700ms vs 900ms debounce inconsistency, `searchText` vs `_searchText` naming, `TooltipService` vs `_tooltipService`).

### 2. Latent NRE in `CaseList.ToggleBookmark` — ✅ Done
[CaseList.razor.cs](../ECTSystem.Web/Pages/CaseList.razor.cs#L353) does `lodCase.BookmarkId!.Value` with no null guard — `MyBookmarks.OnRemoveBookmark` correctly checks. If the projection ever drops `UserId` again (the bug recently fixed), this throws NRE instead of a friendly notification. Add the same guard.

### 3. Stop the redundant `IsBookmarkedAsync` round-trip on right-click — ✅ Done
[CaseList.razor.cs](../ECTSystem.Web/Pages/CaseList.razor.cs#L656) calls `BookmarkService.IsBookmarkedAsync(lodCase.Id)` to decide the menu label — but `lodCase.IsBookmarked` is already populated from the grid load. Use the row data; remove the extra HTTP call (one per right-click).

### 4. Trim `BuildFilter` — ~50 OR clauses is a server killer
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

### 6. URL-sync filters and search
Neither page persists `searchText`, `_workflowStateFilter`, etc. to the query string. Users can't bookmark a filtered view, share a link, or use browser back/forward to recover state. Use `NavigationManager.GetUriWithQueryParameters` + `[SupplyParameterFromQuery]`.

### 7. Cancel debounced searches on dispose — ✅ Done
`Dispose` cancels `_searchCts`, but the `Task.Delay` continuation that survives cancellation calls `_grid.FirstPage(true)` on a disposed component if cancellation races. Move the `_grid.FirstPage` call inside the `try` block after `await Task.Delay`, then check `token.IsCancellationRequested` before calling.

### 8. Make `LineOfDutyCaseMapper.ToCaseListItem` projection-friendly
The recently fixed bug (UserId stripped → bookmark predicate silently fails) is structural: the mapper takes a denormalized graph and re-filters client-side. Either:
- Push the per-user lookup into a stronger DTO contract (a single `Bookmark? CurrentUserBookmark` projected server-side), so `BookmarkId = source.CurrentUserBookmark?.Id` doesn't depend on a hidden column being present.
- Or have the mapper throw/log when `Bookmarks.Any() && all UserId == null` to surface the projection mistake immediately.

### 9. Selection logic auto-selects on every page change
`if (firstItem != null && !_selectedCases.Any(c => c.Id == firstItem.Id)) _selectedCases = [firstItem];` runs on every `LoadData`, so changing filter/page silently re-selects row 1 even if the user had clicked nothing. Either keep selection across loads or only auto-select on the very first load (`!_initialLoadComplete`).

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

### 15. `MyBookmarks` filters all `WorkflowState` values
The dropdown shows every workflow state, including `Draft` — which by definition can't be bookmarked by other users. Consider scoping the filter list to states actually present in the user's bookmarks (one-time `GET /cases?$apply=…` on init).

### 16. Accessibility
- `ContextMenuItem` has no keyboard activation guarantee — verify `aria-haspopup` / `aria-expanded`.
- The search tooltip uses `Duration = null` (sticky) but isn't `aria-describedby`-linked to the search input.
- Bookmark icon-only buttons need `Title` / `aria-label`.

---

## Recommended order
1. ~~**#2 (NRE fix)** + **#3 (drop redundant `IsBookmarked` call)** — quick wins.~~ ✅ Done
2. **#1 (extract base class)** — prevents future drift.
3. **#4 (trim `BuildFilter`)** — performance.

## Status
- ✅ Applied: #2, #3, #5, #7, #10, #11, #12, #13, #14
- ⏳ Remaining: #1 (base class), #4 (trim BuildFilter), #6 (URL-sync), #8 (mapper hardening), #9 (selection logic), #15 (workflow state scoping), #16 (a11y)
