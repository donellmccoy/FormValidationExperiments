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
    API->>DB: Split query (10 includes via AsSplitQuery)
    DB-->>API: Case + all nav properties
    API-->>CS: LineOfDutyCase
    CS-->>UI: LineOfDutyCase

    UI->>SM: StateMachineFactory.Create(case)
    UI->>UI: MapToViewModel + TakeSnapshots

    par Parallel calls
        UI->>BS: IsBookmarkedAsync(caseId)
        BS->>API: GET /odata/CaseBookmarks?$filter=...&$top=1
        API-->>BS: bool
        BS-->>UI: isBookmarked
    and
        UI->>CS: GetCasesAsync(memberId filter, select 5 fields)
        CS->>API: GET /odata/Cases?$filter=MemberId eq {id}&$select=Id,CaseId,Unit,...
        API-->>CS: previous cases
        CS-->>UI: previous cases
        UI->>BS: GetBookmarkedCaseIdsAsync(ids)
        BS->>API: GET /odata/CaseBookmarks?$filter=CaseId in (...)
        API-->>BS: bookmarked IDs
        BS-->>UI: bookmarked IDs
    end
```

**Total: 4 HTTP calls** (1 case+expand, 1 bookmark check, 1 previous cases, 1 bookmarked IDs)

## 1B. Save Tab Data (`SaveTabFormDataAsync`)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant CS as CaseService
    participant AS as AuthorityService
    participant API as OData API

    UI->>UI: ApplyToCase(viewModel → entity)
    UI->>UI: Capture authoritiesToSave reference

    UI->>CS: SaveCaseAsync(case)
    CS->>API: PATCH /odata/Cases({id})<br/>Delta body (scalar props only)
    API-->>CS: Updated case (IncludeAllNavigations)
    CS-->>UI: saved case (replaces _lineOfDutyCase)

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
    API-->>CS: batch response
    CS->>API: GET /odata/Cases?$filter=...&$expand=FullExpand
    Note over CS,API: Re-fetches entire case to get<br/>computed CurrentWorkflowState
    API-->>CS: Full case with all nav props
    CS-->>SM: CaseTransitionResponse
    SM-->>UI: StateMachineResult

    UI->>UI: Re-map entity→VM + TakeSnapshots + navigate to target tab
```

**Total: 2 HTTP calls** (1 $batch history, 1 full case re-fetch)

## 1D. New Case Creation (`OnMemberForwardClick`)

```mermaid
sequenceDiagram
    participant UI as EditCase
    participant SM as StateMachine
    participant CS as CaseService
    participant API as OData API

    UI->>UI: LineOfDutyCaseFactory.Create(memberId)
    UI->>SM: FireAsync(case, ForwardToMemberInformationEntry)
    SM->>CS: TransitionCaseAsync (POST $batch + GET re-fetch)
    CS-->>SM: transition 1 result
    SM-->>UI: result

    UI->>SM: FireAsync(case, ForwardToMedicalTechnician)
    SM->>CS: TransitionCaseAsync (POST $batch + GET re-fetch)
    CS-->>SM: transition 2 result
    SM-->>UI: result
```

**Total: 4 HTTP calls** (2 $batch + 2 re-fetches) — the double transition moves the case from Draft → MemberInformationEntry → MedicalTechnicianReview

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
| Forward to next step | 2 | 9 |
| (optional re-load after navigation) | 4 | 13 |
