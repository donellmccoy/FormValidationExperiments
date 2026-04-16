# Phase 6: Blazor WASM Client — Migrate Write Operations to HttpClient + DTOs

## Overview

Phase 6 migrates all remaining OData `DataServiceContext` write operations in the Blazor WASM client services to use `HttpClient` with strongly-typed DTOs. Read operations continue to use the OData `DataServiceContext` (`EctODataContext`) for query composition (`$filter`, `$orderby`, `$expand`, `$top`, `$skip`, `$count`).

**Why:** The server-side controllers (Phases 1–5) already accept DTOs for POST/PATCH endpoints. The client still sends raw entity models via OData context for most writes, creating friction:
- `SaveCaseAsync` requires a 9-navigation-property preservation hack because `UpdateObject` nulls them during serialization.
- `SaveAuthoritiesAsync` implements a complex query → diff → batch-save pattern against the OData context.
- Several methods attach stub entities or detach tracked entities to work around `DataServiceContext` state management.

Switching writes to `HttpClient.PostAsJsonAsync` / `PatchAsJsonAsync` / `DeleteAsync` with DTOs eliminates all of these workarounds.

---

## Architecture Decision

| Concern | Approach |
|---------|----------|
| **Reads** | Keep `EctODataContext` (OData `DataServiceContext`) — provides `$filter`, `$expand`, `$count` query composition for `RadzenDataGrid` |
| **Writes** | Migrate to `HttpClient` with DTOs — clean request/response, no entity tracking issues |
| **Base class** | `ODataServiceBase` already exposes both `Context` (OData) and `HttpClient` — no DI changes needed |
| **Serialization** | Use existing `ODataServiceBase.JsonOptions` (camelCase, `JsonStringEnumConverter`) |
| **API routes** | Target OData convention routes (`/odata/Cases`, `/odata/Bookmarks`, etc.) — controllers accept DTOs via `[FromBody]` |

---

## Current State Inventory

### Methods Already Using HttpClient (No Change Required)

| Service | Method | HTTP Verb | Notes |
|---------|--------|-----------|-------|
| `CaseService` | `CheckOutCaseAsync` | POST | OData action `/odata/Cases({id})/Checkout` |
| `CaseService` | `CheckInCaseAsync` | POST | OData action `/odata/Cases({id})/Checkin` |
| `CaseService` | `GetCasesByCurrentStateAsync` | POST | OData action `/odata/Cases/ByCurrentState` |
| `CaseDialogueService` | `GetCommentsAsync` | GET | `/odata/CaseDialogueComments?$filter=...` |
| `CaseDialogueService` | `AcknowledgeAsync` | PATCH | `/odata/CaseDialogueComments({id})` |
| `DocumentService` | `UploadDocumentAsync` | POST | Multipart `/odata/Cases({caseId})/Documents` |
| `DocumentService` | `GetForm348PdfAsync` | GET | `/odata/Cases({caseId})/Form348` |

### Methods Requiring Migration (10 total across 6 services)

| # | Service | Method | Current Pattern | Target | DTO | Complexity |
|---|---------|--------|----------------|--------|-----|------------|
| 1 | `BookmarkService` | `AddBookmarkAsync` | `Context.AddObject` | `HttpClient POST` | `CreateBookmarkDto` (exists) | LOW |
| 2 | `BookmarkService` | `RemoveBookmarkAsync` | Query → `Context.DeleteObject` | `HttpClient DELETE` | N/A | LOW |
| 3 | `DocumentService` | `DeleteDocumentAsync` | Stub attach → `Context.DeleteObject` | `HttpClient DELETE` | N/A | LOW |
| 4 | `CaseDialogueService` | `PostCommentAsync` | `Context.AddObject` | `HttpClient POST` | `CaseDialogueComment` entity (controller accepts entity) | LOW |
| 5 | `WorkflowHistoryService` | `AddHistoryEntryAsync` | `Context.AddObject` | `HttpClient POST` | `CreateWorkflowStateHistoryDto` (exists) | LOW |
| 6 | `WorkflowHistoryService` | `AddHistoryEntriesAsync` | Batch `Context.AddObject` + `UseJsonBatch` | `HttpClient POST` (loop or batch endpoint) | `CreateWorkflowStateHistoryDto` (exists) | MEDIUM |
| 7 | `WorkflowHistoryService` | `UpdateHistoryEndDateAsync` | Query → `Context.UpdateObject` | `HttpClient PATCH` | OData Delta or inline JSON | MEDIUM |
| 8 | `AuthorityService` | `SaveAuthoritiesAsync` | Query → diff → batch upsert/delete | Decompose into individual HTTP calls | `CreateAuthorityDto` (exists) + DELETE | HIGH |
| 9 | `CaseService` | `SaveCaseAsync` (create) | `Context.AddObject` + detach | `HttpClient POST` | `CreateCaseDto` (exists) | HIGH |
| 10 | `CaseService` | `SaveCaseAsync` (update) | Nav-prop hack → `Context.UpdateObject` | `HttpClient PATCH` + ETag header | `UpdateCaseDto` (exists) | HIGH |

---

## New DTOs Required

No new shared DTOs are needed — the `CaseDialogueCommentsController.Post` accepts the raw `CaseDialogueComment` entity, and the `WorkflowStateHistoryController.Patch` accepts OData `Delta<WorkflowStateHistory>`. The client can send a partial JSON object for the PATCH (only `ExitDate`).

### Summary of Existing DTOs

| DTO | Location | Properties | Used By |
|-----|----------|------------|---------|
| `CreateCaseDto` | `Shared/ViewModels/CreateCaseDto.cs` | 14 props (MemberId, ProcessType, Component, member info, incident info) | `CasesController.Post` |
| `UpdateCaseDto` | `Shared/ViewModels/UpdateCaseDto.cs` | ~60 props (all case sections) | `CasesController.Patch` |
| `CreateAuthorityDto` | `Shared/ViewModels/CreateAuthorityDto.cs` | 8 props (LineOfDutyCaseId, Role, Name, Rank, Title, ActionDate, Recommendation, Comments) | `AuthoritiesController.Post` |
| `CreateBookmarkDto` | `Shared/ViewModels/CreateBookmarkDto.cs` | 1 prop (LineOfDutyCaseId) | `BookmarksController.Post` |
| `CreateWorkflowStateHistoryDto` | `Shared/ViewModels/CreateWorkflowStateHistoryDto.cs` | 4 props (LineOfDutyCaseId, WorkflowState, EnteredDate, ExitDate?) | `WorkflowStateHistoryController.Post` |

---

## Implementation Plan

### Phase 6A: LOW Complexity Migrations (5 methods)

These are simple create/delete operations with no batching, diffing, or concurrency concerns.

#### 6A.1 — BookmarkService.AddBookmarkAsync

**Current code:**
```csharp
public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    var bookmark = new Bookmark { LineOfDutyCaseId = caseId };

    try
    {
        Context.AddObject("Bookmarks", bookmark);
        await Context.SaveChangesAsync(SaveChangesOptions.None, cancellationToken);
    }
    finally
    {
        Context.Detach(bookmark);
    }
}
```

**Target code:**
```csharp
public async Task AddBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    var dto = new CreateBookmarkDto { LineOfDutyCaseId = caseId };
    var response = await HttpClient.PostAsJsonAsync("odata/Bookmarks", dto, JsonOptions, cancellationToken);
    response.EnsureSuccessStatusCode();
}
```

**Changes:** Replace `Context.AddObject` / `SaveChangesAsync` / `Detach` with `HttpClient.PostAsJsonAsync`. Use existing `CreateBookmarkDto`. Method returns `Task` (void) — no response deserialization needed.

---

#### 6A.2 — BookmarkService.RemoveBookmarkAsync

**Current code:**
```csharp
public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    var query = Context.Bookmarks
        .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
        .AddQueryOption("$top", 1)
        .AddQueryOption("$select", "Id");

    var bookmarks = await ExecuteQueryAsync(query, cancellationToken);
    var bookmark = bookmarks.FirstOrDefault();

    if (bookmark is null || bookmark.Id == 0)
    {
        return;
    }

    try
    {
        if (Context.GetEntityDescriptor(bookmark) == null)
        {
            Context.AttachTo("Bookmarks", bookmark);
        }
        Context.DeleteObject(bookmark);
        await Context.SaveChangesAsync(cancellationToken);
    }
    catch (DataServiceRequestException ex) when (ex.InnerException is DataServiceClientException { StatusCode: 404 })
    {
        // Already deleted on the server, safely ignore
    }
    finally
    {
        Context.Detach(bookmark);
    }
}
```

**Target code:**
```csharp
public async Task RemoveBookmarkAsync(int caseId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

    // Query still uses OData context to find the bookmark ID
    var query = Context.Bookmarks
        .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
        .AddQueryOption("$top", 1)
        .AddQueryOption("$select", "Id");

    var bookmarks = await ExecuteQueryAsync(query, cancellationToken);
    var bookmark = bookmarks.FirstOrDefault();

    if (bookmark is null || bookmark.Id == 0)
    {
        return;
    }

    // Detach from OData context if tracked (no longer needed for delete)
    if (Context.GetEntityDescriptor(bookmark) != null)
    {
        Context.Detach(bookmark);
    }

    var response = await HttpClient.DeleteAsync($"odata/Bookmarks({bookmark.Id})", cancellationToken);
    if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return;
    response.EnsureSuccessStatusCode();
}
```

**Changes:** Replace `Context.DeleteObject` / `SaveChangesAsync` / `Detach` + exception handling with `HttpClient.DeleteAsync`. Keep OData query for lookup. Detach tracked entity before HttpClient call.

---

#### 6A.3 — DocumentService.DeleteDocumentAsync

**Current code:**
```csharp
public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);

    var documentUri = new Uri(Context.BaseUri, $"Documents({documentId})");
    var trackedDocument = Context.Entities.FirstOrDefault(e => e.Identity == documentUri)?.Entity as LineOfDutyDocument;

    if (trackedDocument != null)
    {
        Context.DeleteObject(trackedDocument);
    }
    else
    {
        var documentToDelete = new LineOfDutyDocument { Id = documentId };
        Context.AttachTo("Documents", documentToDelete);
        Context.DeleteObject(documentToDelete);
    }

    await Context.SaveChangesAsync(cancellationToken);
}
```

**Target code:**
```csharp
public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);

    var response = await HttpClient.DeleteAsync($"odata/Documents({documentId})", cancellationToken);
    response.EnsureSuccessStatusCode();
}
```

**Changes:** Eliminate stub-attach pattern entirely. Single `HttpClient.DeleteAsync` call. Signature keeps both `caseId` and `documentId` parameters for interface compatibility.

---

#### 6A.4 — CaseDialogueService.PostCommentAsync

**Current code:**
```csharp
public async Task<CaseDialogueComment> PostCommentAsync(CaseDialogueComment comment)
{
    Context.AddObject("CaseDialogueComments", comment);
    await Context.SaveChangesAsync();
    Context.Detach(comment);
    return comment;
}
```

**Target code:**
```csharp
public async Task<CaseDialogueComment> PostCommentAsync(CaseDialogueComment comment)
{
    var response = await HttpClient.PostAsJsonAsync("odata/CaseDialogueComments", comment, JsonOptions);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<CaseDialogueComment>(JsonOptions);
}
```

**Changes:** Replace `Context.AddObject` / `SaveChangesAsync` / `Detach` with `HttpClient.PostAsJsonAsync`. The controller already accepts the `CaseDialogueComment` entity directly.

---

#### 6A.5 — WorkflowHistoryService.AddHistoryEntryAsync

**Current code:**
```csharp
public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry)
{
    Context.AddObject("WorkflowStateHistory", entry);
    await Context.SaveChangesAsync();
    Context.Detach(entry);
    return entry;
}
```

**Target code:**
```csharp
public async Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry)
{
    var dto = new CreateWorkflowStateHistoryDto
    {
        LineOfDutyCaseId = entry.LineOfDutyCaseId,
        WorkflowState = entry.WorkflowState,
        EnteredDate = entry.EnteredDate,
        ExitDate = entry.ExitDate
    };
    var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions);
}
```

**Changes:** Map to `CreateWorkflowStateHistoryDto` and POST via HttpClient.

---

### Phase 6B: MEDIUM Complexity Migrations (2 methods)

These involve batch operations or partial updates that require more careful handling.

#### 6B.1 — WorkflowHistoryService.AddHistoryEntriesAsync (batch)

**Current code:**
```csharp
public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries)
{
    foreach (var entry in entries)
    {
        Context.AddObject("WorkflowStateHistory", entry);
    }

    var response = await Context.SaveChangesAsync(SaveChangesOptions.BatchWithSingleChangeset | SaveChangesOptions.UseJsonBatch);

    var saved = new List<WorkflowStateHistory>();
    foreach (var op in response)
    {
        if (op is ChangeOperationResponse { Descriptor: EntityDescriptor { Entity: WorkflowStateHistory h } })
        {
            Context.Detach(h);
            saved.Add(h);
        }
    }

    return saved;
}
```

**Target code (Option A — sequential POSTs):**
```csharp
public async Task<List<WorkflowStateHistory>> AddHistoryEntriesAsync(List<WorkflowStateHistory> entries)
{
    var saved = new List<WorkflowStateHistory>();

    foreach (var entry in entries)
    {
        var dto = new CreateWorkflowStateHistoryDto
        {
            LineOfDutyCaseId = entry.LineOfDutyCaseId,
            WorkflowState = entry.WorkflowState,
            EnteredDate = entry.EnteredDate,
            ExitDate = entry.ExitDate
        };
        var response = await HttpClient.PostAsJsonAsync("odata/WorkflowStateHistory", dto, JsonOptions);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions);
        saved.Add(created);
    }

    return saved;
}
```

**Target code (Option B — batch endpoint, future enhancement):**
Add a `PostBatch` action on `WorkflowStateHistoryController` that accepts `List<CreateWorkflowStateHistoryDto>` and returns `List<WorkflowStateHistory>`. This is preferred for atomicity but requires a server-side change.

**Recommendation:** Start with Option A (sequential POSTs). Batch counts are typically small (1–3 entries per state transition). Add a batch endpoint later if performance profiling shows need.

---

#### 6B.2 — WorkflowHistoryService.UpdateHistoryEndDateAsync

**Current code:**
```csharp
public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

    var query = Context.WorkflowStateHistories
        .AddQueryOption("$filter", $"Id eq {entryId}")
        .AddQueryOption("$top", 1);

    var results = await ExecuteQueryAsync(query, cancellationToken);
    var entry = results.FirstOrDefault()
        ?? throw new InvalidOperationException($"Workflow state history entry {entryId} not found.");

    entry.ExitDate = endDate;

    if (Context.GetEntityDescriptor(entry) == null)
    {
        Context.AttachTo("WorkflowStateHistory", entry);
    }

    Context.UpdateObject(entry);
    await Context.SaveChangesAsync(cancellationToken);
    Context.Detach(entry);

    return entry;
}
```

**Target code:**
```csharp
public async Task<WorkflowStateHistory> UpdateHistoryEndDateAsync(int entryId, DateTime endDate, CancellationToken cancellationToken = default)
{
    ArgumentOutOfRangeException.ThrowIfNegativeOrZero(entryId);

    // The controller's PATCH extracts only ExitDate from the delta.
    // Send a minimal JSON body with just ExitDate.
    var patchBody = new { ExitDate = endDate };
    var response = await HttpClient.PatchAsJsonAsync($"odata/WorkflowStateHistory({entryId})", patchBody, JsonOptions, cancellationToken);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<WorkflowStateHistory>(JsonOptions, cancellationToken)
        ?? throw new InvalidOperationException($"Workflow state history entry {entryId} not found.");
}
```

**Changes:** Eliminate OData query + `UpdateObject` pattern. Send a minimal PATCH with only `ExitDate`. Returns `Task<WorkflowStateHistory>` to match existing interface. The server's `Patch` method already validates that `ExitDate` is the only mutable field.

**Note:** The server endpoint uses OData `Delta<WorkflowStateHistory>` which deserializes from a plain JSON body. Sending `{ "ExitDate": "2024-01-15T00:00:00Z" }` works with the OData Delta binder.

---

### Phase 6C: HIGH Complexity Migrations (3 methods)

These involve batch upsert logic, navigation property management, or concurrency control.

#### 6C.1 — AuthorityService.SaveAuthoritiesAsync

**Current code (summary):**
1. Query existing authorities for the case via OData
2. Diff by `Role` — identify authorities to delete, update, and add
3. `Context.DeleteObject` for removed authorities
4. Direct property assignment + `Context.UpdateObject` for matched authorities
5. `Context.AddObject` for new authorities with `LineOfDutyCaseId = caseId`
6. `Context.SaveChangesAsync(BatchWithSingleChangeset | UseJsonBatch)`
7. Detach all tracked entities

**Target code:**
```csharp
public async Task SaveAuthoritiesAsync(int caseId, List<LineOfDutyAuthority> authorities)
{
    // 1. Query existing authorities (keep OData read)
    var query = (DataServiceQuery<LineOfDutyAuthority>)Context.Authorities
        .Where(a => a.LineOfDutyCaseId == caseId);
    var existing = (await query.ExecuteAsync()).ToList();

    var existingByRole = existing.ToDictionary(a => a.Role);
    var incomingByRole = authorities.ToDictionary(a => a.Role);

    // 2. Delete removed authorities
    var toDelete = existing.Where(e => !incomingByRole.ContainsKey(e.Role));
    foreach (var authority in toDelete)
    {
        var response = await HttpClient.DeleteAsync($"odata/Authorities({authority.Id})");
        response.EnsureSuccessStatusCode();
        if (Context.Entities.Any(ed => ed.Entity == authority))
            Context.Detach(authority);
    }

    // 3. Update matched authorities
    foreach (var incoming in authorities)
    {
        if (existingByRole.TryGetValue(incoming.Role, out var match))
        {
            // PATCH with changed properties
            var patchBody = new
            {
                incoming.Name,
                incoming.Rank,
                incoming.Title,
                incoming.ActionDate,
                incoming.Recommendation,
                incoming.Comments
            };
            var response = await HttpClient.PatchAsJsonAsync($"odata/Authorities({match.Id})", patchBody, JsonOptions);
            response.EnsureSuccessStatusCode();
            if (Context.Entities.Any(ed => ed.Entity == match))
                Context.Detach(match);
        }
        else
        {
            // 4. Add new authorities
            var dto = new CreateAuthorityDto
            {
                LineOfDutyCaseId = caseId,
                Role = incoming.Role,
                Name = incoming.Name,
                Rank = incoming.Rank,
                Title = incoming.Title,
                ActionDate = incoming.ActionDate,
                Recommendation = incoming.Recommendation,
                Comments = incoming.Comments?.ToList() ?? []
            };
            var response = await HttpClient.PostAsJsonAsync("odata/Authorities", dto, JsonOptions);
            response.EnsureSuccessStatusCode();
        }
    }
}
```

**Changes:**
- Read via OData stays (for diff).
- Delete → `HttpClient.DeleteAsync` per authority.
- Update → `HttpClient.PatchAsJsonAsync` per authority (the controller uses `Delta<LineOfDutyAuthority>`).
- Add → `HttpClient.PostAsJsonAsync` with `CreateAuthorityDto`.
- Eliminate batch `SaveChangesAsync` and all `Detach` calls.
- Detach any remaining OData-tracked entities from the read query.

**Trade-off:** This sends N individual HTTP requests instead of one OData batch request. Authority counts per case are small (typically 3–5). If atomicity is required, add a server-side `UpsertAuthorities` action that wraps the entire operation in a transaction.

---

#### 6C.2 — CaseService.SaveCaseAsync (CREATE path)

**Current code:**
```csharp
// CREATE path
if (lodCase.Id == 0)
{
    Context.AddObject("Cases", lodCase);
    await Context.SaveChangesAsync();
    Context.Detach(lodCase);
    return lodCase;
}
```

**Target code:**
```csharp
if (lodCase.Id == 0)
{
    var dto = new CreateCaseDto
    {
        MemberId = lodCase.MemberId,
        ProcessType = lodCase.ProcessType,
        Component = lodCase.Component,
        MemberName = lodCase.Member?.FullName ?? string.Empty,
        MemberRank = lodCase.Member?.Rank.ToString() ?? string.Empty,
        ServiceNumber = lodCase.Member?.ServiceNumber ?? string.Empty,
        MemberDateOfBirth = lodCase.Member?.DateOfBirth ?? default,
        Unit = lodCase.Member?.Unit ?? string.Empty,
        FromLine = lodCase.FromLine,
        IncidentType = lodCase.IncidentType,
        IncidentDate = lodCase.IncidentDate,
        IncidentDescription = lodCase.IncidentDescription,
        IncidentDutyStatus = lodCase.IncidentDutyStatus
    };
    var response = await HttpClient.PostAsJsonAsync("odata/Cases", dto, JsonOptions);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<LineOfDutyCase>(JsonOptions);
}
```

**Changes:** Map entity to `CreateCaseDto` and POST via HttpClient. Eliminates `Context.AddObject` / `SaveChangesAsync` / `Detach`. Server generates `CaseId` and initial workflow state history entry.

**Note on mapping:** The caller (`EditCase.razor.cs`) must ensure the `CreateCaseDto` properties are populated from the form models. Consider whether the caller should construct the DTO directly instead of building a `LineOfDutyCase` entity first — this is a follow-up refactoring opportunity.

---

#### 6C.3 — CaseService.SaveCaseAsync (UPDATE path)

**Current code (summary):**
1. Capture 9 navigation properties (Documents, Authorities, Appeals, Member, MEDCON, INCAP, Notifications, WitnessStatements, AuditComments)
2. Null them out on the entity
3. `Context.UpdateObject(lodCase)`
4. `Context.SaveChangesAsync()`
5. Restore navigation properties from captured copies
6. `Context.Detach(lodCase)`

**Target code:**
```csharp
// UPDATE path
var dto = CaseDtoMapper.ToUpdateDto(lodCase); // NEW mapper method needed (entity → DTO)

var request = new HttpRequestMessage(HttpMethod.Patch, $"odata/Cases({lodCase.Id})")
{
    Content = JsonContent.Create(dto, options: JsonOptions)
};

// ETag for optimistic concurrency — RowVersion is the server's concurrency token
if (lodCase.RowVersion is { Length: > 0 })
{
    request.Headers.IfMatch.Add(
        new System.Net.Http.Headers.EntityTagHeaderValue(
            $"\"{Convert.ToBase64String(lodCase.RowVersion)}\""));
}

var response = await HttpClient.SendAsync(request);
response.EnsureSuccessStatusCode();

var updated = await response.Content.ReadFromJsonAsync<LineOfDutyCase>(JsonOptions);

// Capture the new RowVersion (ETag) for subsequent updates
if (response.Headers.ETag is not null)
{
    var raw = response.Headers.ETag.Tag?.Trim('"');
    if (raw is not null)
    {
        updated.RowVersion = Convert.FromBase64String(raw);
    }
}

return updated;
```

**Changes:**
- Eliminate the entire 9-navigation-property capture/restore hack.
- Map entity to `UpdateCaseDto` (requires a new `CaseDtoMapper.ToUpdateDto(LineOfDutyCase)` mapper method, or the caller constructs the DTO directly).
- Send `If-Match` header with Base64-encoded `RowVersion` for optimistic concurrency.
- Read updated `RowVersion` from response `ETag` header.
- No `Context.UpdateObject` / `Detach` needed.

**New mapper method required:**
```csharp
// In ECTSystem.Shared/Mapping/CaseDtoMapper.cs
public static UpdateCaseDto ToUpdateDto(LineOfDutyCase entity)
{
    return new UpdateCaseDto
    {
        // Map all ~60 scalar properties from entity to DTO
        ProcessType = entity.ProcessType,
        Component = entity.Component,
        // ... (all UpdateCaseDto properties)
    };
}
```

---

### Phase 6D: Interface Updates

Update service interfaces to reflect any parameter type changes. Most methods keep the same signatures since the DTO conversion happens inside the service implementation. However, consider these changes:

| Interface | Method | Change |
|-----------|--------|--------|
| `ICaseService` | `SaveCaseAsync` | Keep `LineOfDutyCase` param for now; service maps to DTO internally. Future: accept DTOs directly. |
| `IAuthorityService` | `SaveAuthoritiesAsync` | No signature change — still accepts `List<LineOfDutyAuthority>`. |
| `IBookmarkService` | `AddBookmarkAsync` / `RemoveBookmarkAsync` | No signature change. |
| `ICaseDialogueService` | `PostCommentAsync` | No signature change — still accepts `CaseDialogueComment`. |
| `IDocumentService` | `DeleteDocumentAsync` | No signature change. |
| `IWorkflowHistoryService` | `AddHistoryEntryAsync` / `AddHistoryEntriesAsync` | No signature change. |
| `IWorkflowHistoryService` | `UpdateHistoryEndDateAsync` | No signature change. |

**Summary:** No interface changes are required in Phase 6. All DTO mapping is encapsulated inside the service implementations.

---

### Phase 6E: New Mapper Method

Add `ToUpdateDto` to `CaseDtoMapper` in `ECTSystem.Shared/Mapping/CaseDtoMapper.cs`:

```csharp
/// <summary>
/// Maps a <see cref="LineOfDutyCase"/> entity to an <see cref="UpdateCaseDto"/>
/// for sending via HttpClient PATCH.
/// </summary>
public static UpdateCaseDto ToUpdateDto(LineOfDutyCase entity)
{
    return new UpdateCaseDto
    {
        ProcessType = entity.ProcessType,
        Component = entity.Component,
        // ... all scalar properties from UpdateCaseDto
        MemberId = entity.MemberId
    };
}
```

This is the inverse of the existing `ApplyUpdate(UpdateCaseDto dto, LineOfDutyCase entity)` method. Every property in `UpdateCaseDto` should be mapped from the corresponding entity property.

---

## Implementation Order

```
Phase 6A (LOW) ─────────────────────────────────────────────────
  6A.1  BookmarkService.AddBookmarkAsync
  6A.2  BookmarkService.RemoveBookmarkAsync
  6A.3  DocumentService.DeleteDocumentAsync
  6A.4  CaseDialogueService.PostCommentAsync
  6A.5  WorkflowHistoryService.AddHistoryEntryAsync

Phase 6B (MEDIUM) ──────────────────────────────────────────────
  6B.1  WorkflowHistoryService.AddHistoryEntriesAsync
  6B.2  WorkflowHistoryService.UpdateHistoryEndDateAsync

Phase 6C (HIGH) ────────────────────────────────────────────────
  6C.1  AuthorityService.SaveAuthoritiesAsync
  6C.2  CaseService.SaveCaseAsync (CREATE)
  6C.3  CaseService.SaveCaseAsync (UPDATE)

Phase 6D (INTERFACE) ───────────────────────────────────────────
  Verify no interface changes needed (expected: none)

Phase 6E (MAPPER) ──────────────────────────────────────────────
  Add CaseDtoMapper.ToUpdateDto (needed before 6C.3)
```

**Note:** Phase 6E (mapper) must be implemented before Phase 6C.3 (SaveCaseAsync UPDATE). It can be done at any point before that step.

---

## Testing Strategy

### Unit Tests

For each migrated method:

1. **Happy path** — Verify `HttpClient` sends correct verb, URL, and body (use `MockHttpMessageHandler`).
2. **Error handling** — Verify behavior on 404, 409 (concurrency), and 500 responses.
3. **Serialization** — Verify DTO is serialized with camelCase and enum-as-string (matching `JsonOptions`).

### Integration Tests

1. **Round-trip** — POST a DTO via HttpClient, GET the entity via OData context, verify all properties persisted.
2. **Concurrency** — For `SaveCaseAsync` UPDATE: send stale `RowVersion` in `If-Match`, verify 409 response.
3. **Delete idempotency** — For `RemoveBookmarkAsync` and `DeleteDocumentAsync`: verify 404 is handled gracefully.

### Regression Tests

1. Re-run all 137 existing tests to confirm no regressions.
2. Manual smoke test of the full workflow: create case → edit case → add authorities → upload document → delete document → add bookmark → transition workflow state → add dialogue comment.

---

## Risks and Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| OData Delta deserialization from plain JSON | PATCH to `Delta<T>` endpoints may not bind correctly from `HttpClient` JSON | Test Delta binding with camelCase JSON; if needed, add DTO-based PATCH endpoints on server |
| Navigation property deserialization | `ReadFromJsonAsync<LineOfDutyCase>` may not hydrate nav properties from OData response | Only scalar properties are needed from write responses; use OData context GET for full entity refresh if needed |
| Authority batch atomicity | Individual HTTP calls lose transactional atomicity | Authority count is small (3–5); add server-side batch endpoint later if needed |
| OData context stale state | After HttpClient writes, the OData context's entity tracker is stale | Detach entities from OData context after HttpClient writes, or refresh via OData query |
| `RowVersion` / ETag format | Client must Base64-encode `RowVersion` and wrap in quotes for `If-Match` header | Follow existing `CheckOutCaseAsync` pattern which already handles this |

---

## File Impact Summary

| File | Changes |
|------|---------|
| `ECTSystem.Web/Services/CaseService.cs` | Rewrite `SaveCaseAsync` (CREATE + UPDATE paths) |
| `ECTSystem.Web/Services/AuthorityService.cs` | Rewrite `SaveAuthoritiesAsync` |
| `ECTSystem.Web/Services/BookmarkService.cs` | Rewrite `AddBookmarkAsync`, `RemoveBookmarkAsync` |
| `ECTSystem.Web/Services/DocumentService.cs` | Rewrite `DeleteDocumentAsync` |
| `ECTSystem.Web/Services/CaseDialogueService.cs` | Rewrite `PostCommentAsync` |
| `ECTSystem.Web/Services/WorkflowHistoryService.cs` | Rewrite `AddHistoryEntryAsync`, `AddHistoryEntriesAsync`, `UpdateHistoryEndDateAsync` |
| `ECTSystem.Shared/Mapping/CaseDtoMapper.cs` | Add `ToUpdateDto(LineOfDutyCase)` method |
| `ECTSystem.Tests/` | Add/update unit tests for all migrated methods |

---

## Success Criteria

- [ ] All 10 write methods migrated to `HttpClient` + DTOs
- [ ] No `Context.AddObject`, `Context.UpdateObject`, or `Context.DeleteObject` calls remain for write operations
- [ ] `SaveCaseAsync` navigation-property preservation hack eliminated
- [ ] `AuthorityService.SaveAuthoritiesAsync` batch-upsert pattern replaced with individual HTTP calls
- [ ] `DocumentService.DeleteDocumentAsync` stub-attach pattern eliminated
- [ ] All existing 137 tests still pass
- [ ] New unit tests added for each migrated method
- [ ] End-to-end smoke test passes (create → edit → transition → complete)
- [ ] No interface changes required (DTO mapping encapsulated in services)
