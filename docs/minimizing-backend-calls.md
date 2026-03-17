# Part 4: Minimizing Backend Calls

## Current State: Calls Per Operation

| Operation | Current Calls | Minimum Possible |
|---|---|---|
| Load case | 4 | 2 |
| Save tab | 3 | 1 |
| Transition | 2 | 1 |
| New case (2 transitions) | 4 | 2 |
| **Typical edit session** | **~9** | **~4** |

## Recommendation 1: Embed Bookmark Status in Case Response

**Current**: Separate `GET /odata/CaseBookmarks?$filter=CaseId eq '...' and UserId eq '...'` after case load.

**Proposed**: Add a computed/unmapped `IsBookmarkedByCurrentUser` property or return bookmark status via a custom header or an OData annotation on the case response. The API already knows the authenticated user from the JWT.

**Savings**: −1 call on initial load.

**Conventional technique**: OData annotations (`@Computed`) or a bound function returning an enriched projection. ASP.NET Core also supports custom response headers. Both are standard patterns.

## Recommendation 2: Avoid PATCH Response Re-Fetch of Full Graph

**Current**: After `Patch()`, the controller does:
```csharp
var patched = await context.Cases.IncludeAllNavigations().AsNoTracking().FirstAsync(c => c.Id == key, ct);
return Updated(patched);
```

**Proposed**: Return only the patched scalar entity (no includes) or return `204 No Content` with the updated `ETag`. The client already has the navigation properties in memory — it only needs confirmation and the new `RowVersion`.

```csharp
// Option A: Return minimal
return Updated(existing); // no IncludeAllNavigations()

// Option B: Return nothing
Response.Headers.ETag = $"\"{Convert.ToBase64String(existing.RowVersion)}\"";
return NoContent();
```

**Savings**: −10 split SQL queries per save, smaller response payload.

**Convention**: OData `Prefer: return=minimal` header is the standard mechanism for this. The server can respect this preference and return `204` when the client sends the header.

## Recommendation 3: Compute `CurrentWorkflowState` Client-Side After Transitions

**Current**: After `TransitionCaseAsync` batch-saves history entries, it immediately re-fetches the entire case to get `CurrentWorkflowState` (which is just the state from the most recent history entry).

**Proposed**: The client already knows the target state (it drove the state machine). After a successful $batch save of history entries, update `CurrentWorkflowState` locally instead of re-fetching:

```csharp
// In TransitionCaseAsync, after successful batch:
_lineOfDutyCase.WorkflowStateHistories.AddRange(request.HistoryEntries);
// CurrentWorkflowState is computed from the collection — no re-fetch needed
return new CaseTransitionResponse { Case = _lineOfDutyCase };
```

**Savings**: −1 call per transition (eliminates the re-fetch), −11 SQL queries on the API.

**Risk**: The client's case entity may drift from the DB if another user modifies it concurrently. Mitigate with `RowVersion` concurrency checks on the next save.

## Recommendation 4: Combine Authority Save into Case PATCH

**Current**: `SaveTabFormDataAsync` does PATCH case → GET existing authorities → $batch authority upserts (3 calls).

**Proposed**: Include the authorities in a single `$batch` request alongside the case PATCH. OData `$batch` supports mixed entity types in a single changeset, which is already the pattern used for history entries.

Alternatively, skip the GET-then-diff pattern by tracking authority changes client-side via the existing `TrackableModel` snapshot system.

**Savings**: −1 to −2 calls per save.

**Convention**: OData `$batch` with mixed entity types in a changeset is the standard pattern for atomic multi-entity writes.

## Recommendation 5: Merge Double Transition for New Cases

**Current**: New case creation fires two sequential transitions (Draft → MemberInformation → MedicalTechnician), each doing $batch + re-fetch = 4 calls.

**Proposed**: Create a dedicated `InitiateCase` endpoint or a composite state machine transition that produces all history entries in a single batch:

```csharp
// Single batch with 4 history entries:
// Draft Completed, MemberInformation InProgress, MemberInformation Completed, MedTech InProgress
```

**Savings**: −2 calls (4 → 2, or even 4 → 1 with a dedicated endpoint).

**Convention**: Composite operations are standard in domain-driven APIs. An `InitiateCase` action is a cleaner abstraction than leaking the two-step Draft→MemberInfo→MedTech transition to the client.

## Summary: Achievable Minimum

| Operation | Current | Optimized | Savings |
|---|---|---|---|
| Load case | 4 | 3 | Embed bookmark (−1) |
| Save tab | 3 | 1 | Combined $batch (−2) |
| Transition | 2 | 1 | Client-side state (−1) |
| New case | 4 | 1 | Composite action (−3) |
| **Typical session** | **~9** | **~4** | **~56% fewer calls** |
