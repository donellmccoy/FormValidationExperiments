# Plan: Split LineOfDutyCaseHttpService into Separate Services

## Problem

`LineOfDutyCaseHttpService` (726 lines, ~20 public methods) is a monolithic service that handles all API communication — cases, bookmarks, documents, workflow history, members, authorities, and PDF generation. Every consumer depends on the single `IDataService` interface, even when it needs only a fraction of the methods.

## Goals

- **Single Responsibility** — Each service wraps one OData entity set / API controller.
- **Interface Segregation** — Consumers depend only on the methods they use.
- **Maintainability** — Smaller, focused files are easier to navigate and test.

## Proposed Services

### 1. `ICaseService` / `CaseHttpService` (~150 lines)

Maps to: `CasesController`

| Method | Consumers |
|--------|-----------|
| `GetCasesAsync` | Dashboard, CaseList, EditCase |
| `GetCaseAsync` | EditCase |
| `SaveCaseAsync` | EditCase |
| `TransitionCaseAsync` | LineOfDutyStateMachine |
| `CheckOutCaseAsync` | CaseList |
| `CheckInCaseAsync` | EditCase |

Includes: `ScalarProperties` static field, `BuildScalarPatchBody` helper.

### 2. `IBookmarkService` / `BookmarkHttpService` (~120 lines)

Maps to: `CaseBookmarksController`

| Method | Consumers |
|--------|-----------|
| `GetBookmarkedCasesAsync` | Dashboard, MyBookmarks, BookmarkCountService |
| `AddBookmarkAsync` | CaseList, EditCase |
| `RemoveBookmarkAsync` | CaseList, EditCase, MyBookmarks |
| `IsBookmarkedAsync` | CaseList, EditCase |
| `GetBookmarkedCaseIdsAsync` | EditCase |

### 3. `IDocumentService` / `DocumentHttpService` (~100 lines)

Maps to: `DocumentsController` + `DocumentFilesController`

| Method | Consumers |
|--------|-----------|
| `GetDocumentsAsync` (simple) | EditCase.Documents |
| `GetDocumentsAsync` (paged) | EditCase.Documents |
| `UploadDocumentAsync` | (interface — not yet called) |
| `DeleteDocumentAsync` | EditCase.Documents |
| `GetForm348PdfAsync` | EditCase.Form348 |

> `GetForm348PdfAsync` is grouped here because it returns a document (PDF bytes) from a document-related API endpoint.

### 4. `IWorkflowHistoryService` / `WorkflowHistoryHttpService` (~60 lines)

Maps to: `WorkflowStateHistoriesController`

| Method | Consumers |
|--------|-----------|
| `GetWorkflowStateHistoriesAsync` | EditCase |
| `AddHistoryEntryAsync` | (internal — called by AddHistoryEntriesAsync) |
| `AddHistoryEntriesAsync` | (interface — not yet called from pages) |

### 5. `IMemberService` / `MemberHttpService` (~60 lines)

Maps to: `MembersController`

| Method | Consumers |
|--------|-----------|
| `SearchMembersAsync` | EditCase.MemberSearch |

Includes: `RankToPayGrade` static dictionary.

### 6. `IAuthorityService` / `AuthorityHttpService` (~70 lines)

Maps to: `AuthoritiesController`

| Method | Consumers |
|--------|-----------|
| `SaveAuthoritiesAsync` | EditCase |

## Shared Infrastructure

Extract a base class to avoid duplicating HTTP plumbing across all 6 services:

### `ODataServiceBase`

```
ECTSystem.Web/Services/ODataServiceBase.cs
```

Contains:
- `protected ODataClient Client` field
- `protected HttpClient HttpClient` field
- `protected static JsonSerializerOptions ODataJsonOptions` (shared serializer config)
- `protected static string BuildNavigationPropertyUrl(...)` helper
- `protected class ODataCountResponse<T>` (inner helper)
- `protected class ODataResponse<T>` (inner helper)
- Constructor accepting `ODataClient` + `HttpClient`

Each concrete service inherits `ODataServiceBase` and adds only its methods.

## File Layout

```
ECTSystem.Web/Services/
├── ODataServiceBase.cs           # NEW — shared HTTP infrastructure
├── ICaseService.cs               # NEW — case CRUD interface
├── CaseHttpService.cs            # NEW — case CRUD implementation
├── IBookmarkService.cs           # NEW — bookmark interface
├── BookmarkHttpService.cs        # NEW — bookmark implementation
├── IDocumentService.cs           # NEW — document interface
├── DocumentHttpService.cs        # NEW — document implementation
├── IWorkflowHistoryService.cs    # NEW — workflow history interface
├── WorkflowHistoryHttpService.cs # NEW — workflow history implementation
├── IMemberService.cs             # NEW — member search interface
├── MemberHttpService.cs          # NEW — member search implementation
├── IAuthorityService.cs          # NEW — authority CRUD interface
├── AuthorityHttpService.cs       # NEW — authority CRUD implementation
├── BookmarkCountService.cs       # MODIFIED — depends on IBookmarkService
├── IDataService.cs               # DELETED
├── LineOfDutyCaseHttpService.cs  # DELETED
└── ODataServiceResult.cs         # UNCHANGED
```

## DI Registration Changes

**Before** (`ServiceCollectionExtensions.cs`):
```csharp
services.AddScoped<IDataService, LineOfDutyCaseHttpService>();
```

**After**:
```csharp
services.AddScoped<ICaseService, CaseHttpService>();
services.AddScoped<IBookmarkService, BookmarkHttpService>();
services.AddScoped<IDocumentService, DocumentHttpService>();
services.AddScoped<IWorkflowHistoryService, WorkflowHistoryHttpService>();
services.AddScoped<IMemberService, MemberHttpService>();
services.AddScoped<IAuthorityService, AuthorityHttpService>();
```

All services remain **scoped** (matching the current `IDataService` lifetime).

## Consumer Migration

Each consumer replaces `IDataService` with only the interfaces it needs:

| Consumer | Current | After |
|----------|---------|-------|
| **Dashboard.razor.cs** | `IDataService` | `ICaseService` + `IBookmarkService` |
| **CaseList.razor.cs** | `IDataService` | `ICaseService` + `IBookmarkService` |
| **EditCase.razor.cs** | `IDataService` | `ICaseService` + `IBookmarkService` + `IAuthorityService` |
| **EditCase.Documents.razor.cs** | `IDataService` | `IDocumentService` |
| **EditCase.MemberSearch.razor.cs** | `IDataService` | `IMemberService` |
| **EditCase.Form348.razor.cs** | `IDataService` | `IDocumentService` |
| **MyBookmarks.razor.cs** | `IDataService` | `IBookmarkService` |
| **BookmarkCountService** | `IDataService` | `IBookmarkService` |
| **LineOfDutyStateMachineFactory** | `IDataService` | `ICaseService` |
| **LineOfDutyStateMachine** | `IDataService` | `ICaseService` |

> **EditCase partial classes**: Since `EditCase.razor.cs`, `EditCase.Documents.razor.cs`, `EditCase.MemberSearch.razor.cs`, and `EditCase.Form348.razor.cs` are all partial classes of the same `EditCase` component, all injected services are shared across them. The component will inject: `ICaseService`, `IBookmarkService`, `IAuthorityService`, `IDocumentService`, `IMemberService`, `IWorkflowHistoryService`.

## Implementation Order

| Step | Description | Files |
|------|-------------|-------|
| **1** | Create `ODataServiceBase` with shared infrastructure | `ODataServiceBase.cs` |
| **2** | Create `ICaseService` + `CaseHttpService` | `ICaseService.cs`, `CaseHttpService.cs` |
| **3** | Create `IBookmarkService` + `BookmarkHttpService` | `IBookmarkService.cs`, `BookmarkHttpService.cs` |
| **4** | Create `IDocumentService` + `DocumentHttpService` | `IDocumentService.cs`, `DocumentHttpService.cs` |
| **5** | Create `IWorkflowHistoryService` + `WorkflowHistoryHttpService` | `IWorkflowHistoryService.cs`, `WorkflowHistoryHttpService.cs` |
| **6** | Create `IMemberService` + `MemberHttpService` | `IMemberService.cs`, `MemberHttpService.cs` |
| **7** | Create `IAuthorityService` + `AuthorityHttpService` | `IAuthorityService.cs`, `AuthorityHttpService.cs` |
| **8** | Update DI registration | `ServiceCollectionExtensions.cs` |
| **9** | Migrate consumers — update `[Inject]` properties and method calls | All page/service files from consumer table |
| **10** | Delete `IDataService.cs` + `LineOfDutyCaseHttpService.cs` | — |
| **11** | Build verification | `dotnet build ECTSystem.slnx` |

Each step should compile independently (steps 2–7 add new files alongside the existing service; step 9 switches consumers; step 10 removes the old code).
