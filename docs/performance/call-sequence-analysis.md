# Part 1: Call Sequence Analysis

## 1A. Initial Case Load (`LoadCaseAsync`)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant SM as StateMachine
    participant CS as CaseService
    participant BS as BookmarkService
    participant API as OData API
    participant DB as SQL Server

    UI->>CS: GetCaseAsync(caseId)
    CS->>API: GET /odata/Cases?$filter=CaseId eq '{id}'&$top=1<br/>&$expand=Authorities,Appeals($expand=AppellateAuthority),<br/>Member,MEDCON,INCAP,Notifications,WorkflowStateHistories
    API->>DB: OData middleware applies $expand to IQueryable<br/>(AsSplitQuery, AsNoTracking)
    DB-->>API: Case + requested nav properties
    API-->>CS: LineOfDutyCase
    CS-->>UI: (LineOfDutyCase, isBookmarked: null — collection GET has no header)

    UI->>SM: StateMachineFactory.Create(case)
    UI->>UI: MapToViewModel + TakeSnapshots
    UI->>UI: Pre-populate _trackingPreloaded from $expand data

    Note over UI: Bookmark status: falls back to separate call<br/>(X-Case-IsBookmarked header only on single-entity GET)

    UI->>BS: CheckBookmarkAsync (fallback)
    BS->>API: GET /odata/CaseBookmarks?$filter=...&$top=1
    API-->>BS: bool
    BS-->>UI: isBookmarked

    UI->>UI: LoadPreviousCasesAsync triggers grid.FirstPage
    Note over UI: Previous cases load via RadzenDataGrid LoadData event
    UI->>CS: GetCasesAsync(memberId filter, select 6 fields)
    CS->>API: GET /odata/Cases?$filter=MemberId eq {id}&$select=Id,CaseId,Unit,...
    API-->>CS: previous cases
    CS-->>UI: previous cases
    UI->>BS: GetBookmarkedCaseIdsAsync(ids)
    BS->>API: GET /odata/CaseBookmarks?$filter=CaseId in (...)
    API-->>BS: bookmarked IDs
    BS-->>UI: bookmarked IDs
```

**Total: 4 HTTP calls** (1 case+expand, 1 bookmark check, 1 previous cases, 1 bookmarked IDs)

> **API-side optimization (single-entity GET `/odata/Cases({key})`):**
> The single-entity GET now uses `SingleResult.Create(query)` — OData middleware applies only
> the client-requested `$expand` instead of eagerly loading all navigations.
> It also supports conditional GET via ETag (RowVersion): a lightweight RowVersion-only query
> determines freshness, returning 304 Not Modified when unmodified. Embeds `X-Case-IsBookmarked`
> response header to avoid a separate bookmark check. The client's `GetCaseAsync` currently
> uses the collection endpoint (`$filter`+`$top=1`), so these optimizations apply only to
> single-entity access patterns (e.g., direct OData URL, future client refactoring).

## 1B. Save Tab Data (`SaveTabFormDataAsync`)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant CS as CaseService
    participant AS as AuthorityService
    participant API as OData API

    UI->>UI: ApplyToCase(viewModel → entity)

    UI->>CS: SaveCaseAsync(case)
    CS->>CS: Capture nav properties (Documents,<br/>Authorities, Appeals, Member, etc.)
    CS->>API: PATCH /odata/Cases({id})<br/>Delta body (scalar props only)
    API-->>CS: Slim response (IncludeWorkflowState only)
    CS->>CS: Restore captured nav properties
    CS-->>UI: same in-memory case with nav props intact

    alt Has authorities
        UI->>AS: SaveAuthoritiesAsync(caseId, authorities)
        AS->>API: GET /odata/Authorities?$filter=LineOfDutyCaseId eq {id}
        API-->>AS: existing authorities
        AS->>AS: Diff by Role (delete removed, upsert changed/new)
        AS->>API: POST /odata/$batch (single changeset)
        API-->>AS: batch response
        AS-->>UI: done
    end

    UI->>UI: Re-map entity→VM + TakeSnapshots
```

**Total: 2–3 HTTP calls** (1 PATCH, 1 GET existing authorities, 1 $batch upserts)

> **Optimization note:** The PATCH response previously used `IncludeAllNavigations()` (~85KB),
> now uses `IncludeWorkflowState()` (scalar fields + WorkflowStateHistories only).
> The client captures and restores all other navigation properties in-memory,
> avoiding data loss without a full re-read.

## 1C. Workflow Transition (`FireWorkflowActionAsync` → state machine)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant SM as StateMachine
    participant CS as CaseService
    participant API as OData API

    UI->>UI: ApplyToCase(viewModel → entity)
    UI->>SM: FireAsync(case, trigger)
    SM->>SM: Build history entries<br/>(Forward: Completed + InProgress)<br/>(Return: N×Returned + InProgress)
    SM->>CS: TransitionCaseAsync(caseId, historyEntries)
    CS->>API: POST /odata/$batch<br/>(WorkflowStateHistory entries)
    API-->>CS: batch response with server-assigned entries
    CS-->>SM: CaseTransitionResponse (history entries only)
    SM->>SM: Merge entries into<br/>case.WorkflowStateHistories
    SM-->>UI: StateMachineResult

    UI->>UI: Re-map entity→VM + TakeSnapshots + navigate to target tab
```

**Total: 1 HTTP call** (1 $batch history — no re-fetch)

> **Optimization note:** Previously made 2 calls (batch + full case re-fetch with `FullExpand`).
> Now the client merges the server-assigned `WorkflowStateHistory` entries in-memory and
> derives `CurrentWorkflowState` from the merged collection, eliminating the re-fetch.

## 1D. New Case Creation (`OnMemberForwardClick`)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant SM as StateMachine
    participant CS as CaseService
    participant API as OData API

    UI->>UI: LineOfDutyCaseFactory.Create(memberId)
    UI->>SM: FireAsync(case, ForwardToMemberInformationEntry)
    SM->>CS: TransitionCaseAsync (POST $batch only)
    CS-->>SM: transition 1 result (history entries)
    SM->>SM: Merge entries in-memory
    SM-->>UI: result

    UI->>SM: FireAsync(case, ForwardToMedicalTechnician)
    SM->>CS: TransitionCaseAsync (POST $batch only)
    CS-->>SM: transition 2 result (history entries)
    SM->>SM: Merge entries in-memory
    SM-->>UI: result
```

**Total: 2 HTTP calls** (2 $batch — no re-fetches) — the double transition moves the case from Draft → MemberInformationEntry → MedicalTechnicianReview

## 1E. Tab-Specific Lazy Loads

| User Action | HTTP Calls | Detail |
|---|---|---|
| **Tracking tab** (first click) | **0** | Uses preloaded `WorkflowStateHistories` from initial `$expand` (`_trackingPreloaded = true`) |
| **Tracking tab** (search/page) | **1** | `GET /odata/WorkflowStateHistories?$filter=...&$orderby=...&$top=...&$skip=...&$count=true` |
| **Previous Cases grid** (page) | **2** | `GET /odata/Cases?$filter=MemberId eq ...&$select=...` + `GET /odata/CaseBookmarks?$filter=...` |
| **Documents grid** (page) | **1** | `GET /odata/Cases({id})/Documents?$filter=...&$top=...&$skip=...&$count=true` |
| **Document download** | **1** | `GET /api/document-files/{docId}` (REST, not OData) |
| **Document upload** | **1** | `POST /api/document-files/{caseId}` (REST multipart) |
| **AF Form 348 PDF** (tab click) | **1** | `GET /api/document-files/{caseId}/form348` (lazy, one-time) |
| **Bookmark toggle** | **1–2** | `POST/DELETE /odata/CaseBookmarks` + `BookmarkCountService.RefreshAsync()` |
| **Member search** | **1** | `GET /odata/Members?$filter=contains(...)&$top=10` (300ms debounced) |
| **Check-in** | **1** | `POST /odata/Cases({id})/Checkin` |

## 1F. Complete Call Inventory (Typical Edit Session)

| Phase | Calls | Cumulative |
|---|---|---|
| Initial load | 4 | 4 |
| Apply Changes (save) | 3 | 7 |
| Forward to next step | 1 | 8 |
| (optional re-load after navigation) | 4 | 12 |

> **vs. pre-optimization totals:** Forward was 2 calls (now 1 — no re-fetch).
> Typical session cumulative reduced from 13 → 12.
> PATCH response payload reduced from ~85KB (`IncludeAllNavigations`) to ~2KB (`IncludeWorkflowState`).
