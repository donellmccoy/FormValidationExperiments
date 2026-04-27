# Plan: Search-as-you-type for Case Dialogue

## Goal
Add a debounced search box at the top of the Case Dialogue tab that filters the visible comment thread (and replies) as the user types.

## Design decisions

1. **Client-side filtering** of already-loaded comments — fast, no API churn, matches the paged "Load older comments" model. Server-side search can be a follow-up if the dataset grows.
2. **Debounce ~300ms** to avoid re-rendering on every keystroke.
3. **Match scope:** comment `Text`, `AuthorName`, `AuthorRole` (case-insensitive, contains). A reply matches if the reply itself matches OR its parent matches (so context is preserved).
4. **UX:** highlight matched terms in the rendered text via a small `<mark>` wrap helper; show a "No matches" empty state; show match count badge; clear-button (`x`) inside the search box.
5. **Group headers** stay only when the group has at least one visible comment.

## Changes

### `EditCase.CaseDialogue.razor.cs`
- Add fields: `private string _commentSearchTerm = string.Empty;`
- Add a `RadzenTextBox`-bound property with debounce: use Radzen's `Change` event with a `System.Timers.Timer` OR simpler — use `RadzenTextBox` with `@bind-Value` + `Change="OnSearchChanged"` and a `CancellationTokenSource`-based debounce in code-behind.
- Refactor `GetDateGroupedComments()` to apply the filter predicate before grouping; drop empty groups.
- Update `GetReplies(parentId)` to: if no search term → unchanged; if search term → return replies where reply matches OR parent matches.
- Add helpers:
  - `bool MatchesSearch(CaseDialogueComment c)`
  - `MarkupString HighlightMatches(string text)` — returns text with `<mark>` around hits (HTML-encode first to prevent XSS).
  - `int GetMatchCount()` for the badge.

### `EditCase.razor` (Case Dialogue tab header bar)
- Insert a `RadzenTextBox` with placeholder `"Search comments…"`, leading `search` icon, trailing clear button. Place it left of the existing refresh button in the header `RadzenStack`.
- Show an `@if (!string.IsNullOrWhiteSpace(_commentSearchTerm))` badge: `"N match(es)"`.
- Replace `Text="@comment.Text"` and `Text="@reply.Text"` with `@((MarkupString)HighlightMatches(comment.Text))` (and similarly for `AuthorName` if desired) — wrap inside a `<span>` since `RadzenText.Text` doesn't accept MarkupString. Use `<RadzenText>` with `ChildContent` instead.
- Add empty-state block when filtered set is empty: `RadzenAlert` with `"No comments match \"{term}\""`.

### Debounce implementation
```csharp
private CancellationTokenSource _searchCts = new();
private async Task OnSearchInput(string value)
{
    _searchCts.Cancel();
    _searchCts = new CancellationTokenSource();
    var token = _searchCts.Token;
    try
    {
        await Task.Delay(300, token);
        _commentSearchTerm = value ?? string.Empty;
        StateHasChanged();
    }
    catch (TaskCanceledException) { }
}
```

## Out of scope (potential follow-ups)
- Server-side search across un-loaded pages.
- Regex / advanced filters (author-only, date range).
- Persisting last search term across tab switches.
- Keyboard shortcut (`Ctrl+F` interception).

## Risk / validation
- HTML-encode before injecting `<mark>` to avoid XSS from comment text.
- Verify Radzen v9 `RadzenTextBox` exposes a debounce-friendly `Change`/`Input` event (it does; `Change` fires on each keystroke when `@bind-Value:event="oninput"` is used).
- Build + visual verification on Case Dialogue tab.
