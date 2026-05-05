# Bookmarks Grid — Default Sort by Most Recent Bookmark

> **Status:** ✅ Complete

## Problem

The `/bookmarks` page (`MyBookmarks.razor`) does not show the most recently bookmarked case at the top of the grid. New bookmarks appear in arbitrary (effectively `Id asc`) order.

## Root Cause

`MyBookmarks.LoadData` calls `CaseService.GetCasesAsync` and forwards `args.OrderBy` unchanged. When no column is sorted, `args.OrderBy` is empty, so the server applies no ordering and returns rows in default DB order. The bookmark's `CreatedDate` is never used.

OData `$orderby` cannot sort by a property inside a collection navigation (`Bookmarks/CreatedDate`), so this must be fixed server-side.

## Approach

Apply default ordering inside the existing collection-bound function `Cases/Default.Bookmarked` so that when the client omits `$orderby`, results are ordered by the per-user bookmark `CreatedDate desc`. Switch the page to call this function (via `BookmarkService.GetBookmarkedCasesAsync`) instead of the generic `CaseService.GetCasesAsync`.

When the user clicks a column header, the grid's explicit `$orderby` overrides the default — standard `[EnableQuery]` behavior.

## Changes

### 1. API — `ECTSystem.Api/Controllers/CasesController.cs` `Bookmarked()`

Add default ordering by max bookmark `CreatedDate` for the current user, with `Id desc` as tiebreaker:

```csharp
var userId = GetAuthenticatedUserId();
var query = context.Cases
    .AsNoTracking()
    .Where(c => c.Bookmarks.Any(b => b.UserId == userId))
    .OrderByDescending(c => c.Bookmarks
        .Where(b => b.UserId == userId)
        .Max(b => (DateTime?)b.CreatedDate))
    .ThenByDescending(c => c.Id);
```

### 2. Web — `ECTSystem.Web/Pages/MyBookmarks.razor.cs`

- In `LoadData`, call `BookmarkService.GetBookmarkedCasesAsync(...)` instead of `CaseService.GetCasesAsync(...)`.
- In `BuildFilter`, remove the now-redundant `Bookmarks/any(b: b/UserId eq '...')` clause (the function already scopes to the current user).

## Risks / Notes

- Correlated subquery `c.Bookmarks.Max(b => b.CreatedDate)`; fine for typical bookmark volumes. A `(UserId, CreatedDate)` index on `Bookmarks` would help if needed later.
- `BookmarkedByCurrentState` action is unaffected.
- Pagination still correct — ordering is applied before `$skip`/`$top` by `[EnableQuery]`.

## Test Plan

Bookmark a new case, navigate to `/bookmarks`, confirm it appears in row 1.
