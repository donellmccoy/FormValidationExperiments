# Case Dialogue Implementation Plan

## Overview

Replace the placeholder content in the **Case Dialogue** outer tab (index 2) on `EditCase.razor` with a comment thread. Users can post comments and reply to comments. All markup and logic is implemented **inline** in `EditCase.razor` / `EditCase.razor.cs` — no shared components. Comments are standalone records tied to a case; they are **not** associated with any workflow state, documents, or system events.

---

## Reference Design

| Section | Description |
|---------|-------------|
| **Header bar** | Title "Case Dialogue", "Export Logs" button |
| **Date group headers** | Sticky labels: "TODAY, OCTOBER 24", "OCTOBER 23", etc. |
| **User comments** | Avatar (initials), name + role, relative timestamp, message body, Reply / Acknowledge actions |
| **Threaded replies** | Indented replies beneath the parent comment (flat — one level only) |
| **Compose area** | TextArea with placeholder, Send button |

---

## Current State

| Asset | Status |
|-------|--------|
| **Case Dialogue tab** (`EditCase.razor` line ~1709) | Placeholder `RadzenText` only |
| `AuditComment` model | Minimal (Id, LineOfDutyCaseId, Text) — needs expansion or replacement |
| Outer tab constants (`EditCase.Form348.razor.cs`) | No constant for Case Dialogue (index 2) |

---

## Implementation Steps

### Phase 1 — Domain Model

#### 1.1 Create `CaseDialogueComment` model

**File:** `ECTSystem.Shared/Models/CaseDialogueComment.cs`

```csharp
namespace ECTSystem.Shared.Models;

public class CaseDialogueComment : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }

    public string Text { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }           // For replies (flat, one level)

    // Denormalized author info for fast rendering
    public string AuthorName { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = string.Empty;

    public bool IsAcknowledged { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public string AcknowledgedBy { get; set; } = string.Empty;
}
```

#### 1.2 Add navigation property to `LineOfDutyCase`

**File:** `ECTSystem.Shared/Models/LineOfDutyCase.cs`

```csharp
public ICollection<CaseDialogueComment> CaseDialogueComments { get; set; } = new HashSet<CaseDialogueComment>();
```

#### 1.3 Create EF configuration

**File:** `ECTSystem.Persistence/Configurations/CaseDialogueCommentConfiguration.cs`

- Map `Text` → `nvarchar(4000)`
- Index on `(LineOfDutyCaseId, CreatedDate DESC)` for chronological queries
- Index on `ParentCommentId` for reply lookups
- Configure FK to `LineOfDutyCase`

#### 1.4 Add EF migration

```
dotnet ef migrations add AddCaseDialogueComments --project ECTSystem.Persistence
```

---

### Phase 2 — API Layer

#### 2.1 Create `CaseDialogueCommentsController`

**File:** `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs`

| Endpoint | Method | Description |
|----------|--------|-------------|
| `GET /odata/CaseDialogueComments?$filter=LineOfDutyCaseId eq {id}&$orderby=CreatedDate desc` | GET | Paginated comments with OData query |
| `POST /odata/CaseDialogueComments` | POST | Create a new comment or reply |
| `PATCH /odata/CaseDialogueComments({id})` | PATCH | Acknowledge / edit a comment |

Follow the existing OData controller pattern in the project. The controller should:
- Accept `ODataQueryOptions<CaseDialogueComment>` for filtering/sorting/paging
- Automatically populate `AuthorName`, `AuthorRole`, `CreatedBy`, `CreatedDate` from the authenticated user
- Restrict updates to acknowledgement and text edits by the original author

#### 2.2 Register in EDM model

**File:** `ECTSystem.Api/Extensions/` (OData model builder)

```csharp
builder.EntitySet<CaseDialogueComment>("CaseDialogueComments");
```

#### 2.3 Register in client EDM model

**File:** `ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs` — `BuildClientEdmModel()`

Add `CaseDialogueComment` as an `EdmEntityType`. No enum registration needed — the model has no enum properties.

---

### Phase 3 — Client Service Layer

#### 3.1 Create `ICaseDialogueService` interface

**File:** `ECTSystem.Web/Services/Interfaces/ICaseDialogueService.cs`

```csharp
public interface ICaseDialogueService
{
    Task<PagedResult<CaseDialogueComment>> GetCommentsAsync(int caseId, int skip, int top, CancellationToken ct);
    Task<CaseDialogueComment> PostCommentAsync(int caseId, string text, int? parentCommentId, CancellationToken ct);
    Task AcknowledgeAsync(int commentId, CancellationToken ct);
}
```

#### 3.2 Create `CaseDialogueService` implementation

**File:** `ECTSystem.Web/Services/CaseDialogueService.cs`

- Extend `ODataServiceBase` following the existing service pattern
- Use `_context.CaseDialogueComments` DataServiceQuery with `$filter`, `$orderby`, `$skip`, `$top`

#### 3.3 Register in DI

**File:** `ECTSystem.Web/Program.cs` (or `ServiceCollectionExtensions.cs`)

```csharp
builder.Services.AddScoped<ICaseDialogueService, CaseDialogueService>();
```

---

### Phase 4 — Inline UI in EditCase

All UI is implemented directly in `EditCase.razor` and `EditCase.razor.cs` — no shared components.

#### 4.1 Add tab constant

**File:** `ECTSystem.Web/Pages/EditCase.Form348.razor.cs`

```csharp
private const int OuterCaseDialogueTabIndex = 2;
```

#### 4.2 Add fields and methods to a separate partial class

**File:** `ECTSystem.Web/Pages/EditCase.CaseDialogue.razor.cs` *(new file — follows the existing partial-class pattern: `EditCase.Documents.razor.cs`, `EditCase.State.razor.cs`, etc.)*

```csharp
[Inject] private ICaseDialogueService CaseDialogueService { get; set; }

// Dialogue state
private List<CaseDialogueComment> _dialogueComments = new();
private string _newCommentText = string.Empty;
private int? _replyToCommentId;
private bool _isLoadingComments;
private int _commentSkip;
private const int CommentPageSize = 20;
private bool _hasMoreComments;

private async Task LoadDialogueCommentsAsync()
{
    _isLoadingComments = true;
    var result = await CaseDialogueService.GetCommentsAsync(
        _lineOfDutyCase.Id, _commentSkip, CommentPageSize, CancellationToken.None);
    _dialogueComments.AddRange(result.Items);
    _hasMoreComments = result.TotalCount > _dialogueComments.Count;
    _isLoadingComments = false;
}

private async Task PostCommentAsync()
{
    if (string.IsNullOrWhiteSpace(_newCommentText)) return;
    var comment = await CaseDialogueService.PostCommentAsync(
        _lineOfDutyCase.Id, _newCommentText.Trim(), _replyToCommentId, CancellationToken.None);
    _dialogueComments.Insert(0, comment);
    _newCommentText = string.Empty;
    _replyToCommentId = null;
}

private async Task AcknowledgeCommentAsync(int commentId)
{
    await CaseDialogueService.AcknowledgeAsync(commentId, CancellationToken.None);
    var comment = _dialogueComments.FirstOrDefault(c => c.Id == commentId);
    if (comment != null) comment.IsAcknowledged = true;
}

private void StartReply(int parentId)
{
    _replyToCommentId = parentId;
    // Optionally focus the compose text area
}

private void CancelReply()
{
    _replyToCommentId = null;
}

private async Task LoadMoreCommentsAsync()
{
    _commentSkip += CommentPageSize;
    await LoadDialogueCommentsAsync();
}

private string FormatRelativeTime(DateTime date) { /* e.g., "2h ago", "Yesterday" */ }

private string GetAuthorInitials(string authorName)
{
    var parts = authorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    return parts.Length >= 2
        ? $"{parts[0][0]}{parts[1][0]}"
        : authorName.Length > 0 ? authorName[..1] : "?";
}

// Group comments by date for display
private IEnumerable<IGrouping<string, CaseDialogueComment>> GetDateGroupedComments()
{
    return _dialogueComments
        .Where(c => c.ParentCommentId == null)  // Top-level only
        .GroupBy(c => c.CreatedDate.Date == DateTime.Today
            ? $"TODAY, {c.CreatedDate:MMMM d}".ToUpperInvariant()
            : c.CreatedDate.ToString("MMMM d").ToUpperInvariant());
}

private IEnumerable<CaseDialogueComment> GetReplies(int parentId)
{
    return _dialogueComments
        .Where(c => c.ParentCommentId == parentId)
        .OrderBy(c => c.CreatedDate);
}
```

#### 4.3 Replace Case Dialogue tab markup

**File:** `ECTSystem.Web/Pages/EditCase.razor` (line ~1709)

Replace the placeholder with inline markup:

```razor
@* ─── Case Dialogue Tab ─── *@
<RadzenTabsItem Text="Case Dialogue">
    <Template>
        <span class="tab-label">Case Dialogue</span>
    </Template>
    <ChildContent>
        <RadzenPanel AllowCollapse="false" Style="width:1135px; height:1125px"
                     class="assessment-panel rz-border-primary-lighter rz-border-radius-2 rz-overflow-hidden">

            @* ── Header bar ── *@
            <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                         JustifyContent="JustifyContent.SpaceBetween"
                         class="rz-p-3 rz-border-bottom rz-border-color-base-700">
                <RadzenText TextStyle="TextStyle.H6" Text="CASE DIALOGUE" />
                <RadzenButton Text="Export Logs" Icon="download"
                              ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small" />
            </RadzenStack>

            @* ── Scrollable comment thread ── *@
            <div style="flex:1; overflow-y:auto; padding:1rem;">

                @if (_hasMoreComments)
                {
                    <RadzenStack JustifyContent="JustifyContent.Center" class="rz-mb-4">
                        <RadzenButton Text="Load older comments" Variant="Variant.Text"
                                      Size="ButtonSize.Small" Click="LoadMoreCommentsAsync"
                                      IsBusy="_isLoadingComments" />
                    </RadzenStack>
                }

                @foreach (var dateGroup in GetDateGroupedComments())
                {
                    @* ── Date separator ── *@
                    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                                 class="rz-my-3" Gap="0.5rem">
                        <hr style="flex:1; border-color:var(--rz-base-700);" />
                        <RadzenText TextStyle="TextStyle.Overline"
                                    Style="color:var(--rz-text-secondary-color);"
                                    Text="@dateGroup.Key" />
                        <hr style="flex:1; border-color:var(--rz-base-700);" />
                    </RadzenStack>

                    @foreach (var comment in dateGroup)
                    {
                        @* ── Comment entry ── *@
                        <RadzenStack Orientation="Orientation.Horizontal" Gap="0.75rem"
                                     class="rz-py-2 rz-border-bottom rz-border-color-base-700">
                            @* Avatar *@
                            <div style="width:36px; height:36px; border-radius:50%;
                                        background:var(--rz-primary); display:flex;
                                        align-items:center; justify-content:center;
                                        font-size:0.8rem; font-weight:600;
                                        color:var(--rz-text-contrast-color); flex-shrink:0;">
                                @GetAuthorInitials(comment.AuthorName)
                            </div>
                            <RadzenStack Gap="0.25rem" Style="flex:1;">
                                @* Author line *@
                                <RadzenStack Orientation="Orientation.Horizontal"
                                             AlignItems="AlignItems.Center" Gap="0.5rem">
                                    <RadzenText TextStyle="TextStyle.Body1"
                                                Style="font-weight:600;"
                                                Text="@comment.AuthorName" />
                                    <RadzenBadge BadgeStyle="BadgeStyle.Info"
                                                 Text="@comment.AuthorRole"
                                                 IsPill="true" Shade="Shade.Dark" />
                                    <RadzenText TextStyle="TextStyle.Caption"
                                                Style="color:var(--rz-text-tertiary-color);"
                                                Text="@FormatRelativeTime(comment.CreatedDate)" />
                                </RadzenStack>
                                @* Message body *@
                                <RadzenText TextStyle="TextStyle.Body2"
                                            Text="@comment.Text" />
                                @* Actions *@
                                <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
                                    <RadzenButton Text="Reply" Icon="reply"
                                                  Variant="Variant.Text" Size="ButtonSize.ExtraSmall"
                                                  Click="@(() => StartReply(comment.Id))" />
                                    @if (!comment.IsAcknowledged)
                                    {
                                        <RadzenButton Text="Acknowledge" Icon="check_circle"
                                                      Variant="Variant.Text" Size="ButtonSize.ExtraSmall"
                                                      Click="@(() => AcknowledgeCommentAsync(comment.Id))" />
                                    }
                                    else
                                    {
                                        <RadzenText TextStyle="TextStyle.Caption"
                                                    Style="color:var(--rz-success);"
                                                    Text="Acknowledged" />
                                    }
                                </RadzenStack>

                                @* ── Replies (indented) ── *@
                                @foreach (var reply in GetReplies(comment.Id))
                                {
                                    <RadzenStack Orientation="Orientation.Horizontal" Gap="0.75rem"
                                                 class="rz-pl-6 rz-pt-2">
                                        <div style="width:28px; height:28px; border-radius:50%;
                                                    background:var(--rz-secondary); display:flex;
                                                    align-items:center; justify-content:center;
                                                    font-size:0.7rem; font-weight:600;
                                                    color:var(--rz-text-contrast-color); flex-shrink:0;">
                                            @GetAuthorInitials(reply.AuthorName)
                                        </div>
                                        <RadzenStack Gap="0.15rem" Style="flex:1;">
                                            <RadzenStack Orientation="Orientation.Horizontal"
                                                         AlignItems="AlignItems.Center" Gap="0.5rem">
                                                <RadzenText TextStyle="TextStyle.Body2"
                                                            Style="font-weight:600;"
                                                            Text="@reply.AuthorName" />
                                                <RadzenText TextStyle="TextStyle.Caption"
                                                            Style="color:var(--rz-text-tertiary-color);"
                                                            Text="@FormatRelativeTime(reply.CreatedDate)" />
                                            </RadzenStack>
                                            <RadzenText TextStyle="TextStyle.Body2"
                                                        Text="@reply.Text" />
                                        </RadzenStack>
                                    </RadzenStack>
                                }
                            </RadzenStack>
                        </RadzenStack>
                    }
                }

                @if (_isLoadingComments)
                {
                    <RadzenStack JustifyContent="JustifyContent.Center" class="rz-p-4">
                        <RadzenProgressBarCircular ShowValue="false" Size="ProgressBarCircularSize.Small" />
                    </RadzenStack>
                }
            </div>

            @* ── Compose area ── *@
            <RadzenStack class="rz-p-3 rz-border-top rz-border-color-base-700" Gap="0.5rem">
                @if (_replyToCommentId != null)
                {
                    <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center"
                                 Gap="0.5rem">
                        <RadzenText TextStyle="TextStyle.Caption"
                                    Style="color:var(--rz-info);"
                                    Text="Replying to comment..." />
                        <RadzenButton Icon="close" Variant="Variant.Text"
                                      Size="ButtonSize.ExtraSmall"
                                      Click="CancelReply" />
                    </RadzenStack>
                }
                <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem"
                             AlignItems="AlignItems.End">
                    <RadzenTextArea @bind-Value="_newCommentText"
                                    Placeholder="Type a comment..."
                                    Style="flex:1;" Rows="2" />
                    <RadzenButton Icon="send" ButtonStyle="ButtonStyle.Primary"
                                  Click="PostCommentAsync"
                                  Disabled="@string.IsNullOrWhiteSpace(_newCommentText)" />
                </RadzenStack>
            </RadzenStack>

        </RadzenPanel>
    </ChildContent>
</RadzenTabsItem>
```

#### 4.4 Scoped CSS additions

**File:** `ECTSystem.Web/Pages/EditCase.razor.css`

Minimal additions if needed for the inline layout. Most styling uses Radzen utility classes and inline styles in the markup above.

---

### Phase 5 — Pagination

- The initial load fetches the first `CommentPageSize` (20) comments via `$orderby=CreatedDate desc&$top=20`
- "Load older comments" button at the top increments `$skip` and appends results
- `_hasMoreComments` is derived from `PagedResult.TotalCount > _dialogueComments.Count`

---

## File Change Summary

| File | Action | Description |
|------|--------|-------------|
| `ECTSystem.Shared/Models/CaseDialogueComment.cs` | **New** | Comment model |
| `ECTSystem.Shared/Models/LineOfDutyCase.cs` | **Edit** | Add `CaseDialogueComments` collection |
| `ECTSystem.Persistence/Configurations/CaseDialogueCommentConfiguration.cs` | **New** | EF config + indexes |
| `ECTSystem.Persistence/ECTSystemDbContext.cs` | **Edit** | Add `DbSet<CaseDialogueComment>` |
| `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs` | **New** | OData CRUD controller |
| `ECTSystem.Api/Extensions/` (OData model builder) | **Edit** | Register `CaseDialogueComments` entity set |
| `ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs` | **Edit** | Add `CaseDialogueComment` to client EDM model |
| `ECTSystem.Web/Services/Interfaces/ICaseDialogueService.cs` | **New** | Service interface |
| `ECTSystem.Web/Services/CaseDialogueService.cs` | **New** | OData service implementation |
| `ECTSystem.Web/Program.cs` | **Edit** | Register `ICaseDialogueService` |
| `ECTSystem.Web/Pages/EditCase.razor` | **Edit** | Replace Case Dialogue placeholder with inline comment thread |
| `ECTSystem.Web/Pages/EditCase.CaseDialogue.razor.cs` | **New** | Separate partial class with dialogue fields, methods, and injected service |
| `ECTSystem.Web/Pages/EditCase.Form348.razor.cs` | **Edit** | Add `OuterCaseDialogueTabIndex` constant |
| `ECTSystem.Web/Pages/EditCase.razor.css` | **Edit** | Minor scoped CSS if needed |

---

## Suggested Implementation Order

```
Phase 1  →  Phase 2  →  Phase 3  →  Phase 4  →  Phase 5
 Model       API       Service    Inline UI    Pagination
```

Phases 1–4 form the **MVP**: a working comment thread with post and reply.
Phase 5 adds load-more pagination for long threads.

---

## Open Questions

1. **Consolidate Comments tab?** The existing Comments tab (index 3) overlaps — remove it and adjust outer tab indexes?
2. **Reply threading depth** — flat (1 level of replies) or nested? Recommend flat for V1.
3. **Real-time updates** — SignalR for live push, or manual refresh? Recommend manual refresh for V1.
4. **Acknowledge semantics** — what does acknowledging a comment mean for the workflow? Is it tracked for compliance?
