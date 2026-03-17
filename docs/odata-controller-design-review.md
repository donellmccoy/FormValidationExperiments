# Part 3: OData Controller Design Review

## Strengths

1. **`[EnableQuery]` with limits** — `MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 500` prevents runaway queries. The collection GET endpoint properly returns `IQueryable` for server-side composition.

2. **ETag/conditional GET** — The single-entity GET uses `RowVersion` as an ETag with `If-None-Match` → 304 responses. This is textbook HTTP caching.

3. **Bound actions for Checkout/Checkin** — Side-effectful operations are modeled as OData Actions (POST semantics), not properties. This is correct OData modeling.

4. **`ByCurrentState` as a bound function** — Complex filtering (subquery on `WorkflowStateHistory`) is encapsulated server-side rather than forcing the client to construct an impossible `$filter`.

5. **Delta\<T\> patching** — The PATCH endpoint uses OData's `Delta<T>` to apply only changed properties, which is the canonical approach.

6. **CaseId generation with UPDLOCK** — Prevents concurrent duplicate suffix assignment. The retry loop handles race conditions gracefully.

## Concerns

1. **`IncludeAllNavigations()` on every response** — The PATCH, POST, and single GET endpoints all re-read the case with 10 `.Include()` calls after mutation. This means:
   - **PATCH** (save scalar fields): Issues 11 SQL queries (1 base + 10 split queries) to return data the client already has
   - **POST** (create): Same 11 queries for a brand-new case with empty collections
   - **TransitionCaseAsync** re-fetch: 11 queries to get back the same entity the client just modified

   **Root cause**: The client needs `CurrentWorkflowState` (derived from `WorkflowStateHistories`) after transitions. But it doesn't need **all** navigation properties refreshed.

   **Recommendation**: Return only what changed. For PATCH, the response could return the patched entity without navigation properties (or with only `WorkflowStateHistories` for state derivation). The client holds the navigation data in memory and only needs confirmation that scalar writes succeeded.

2. **No `$select` on client-side case fetch** — `CaseHttpService.GetCaseAsync()` always requests the full property set. For the case list page, `GetCasesAsync` correctly uses `$select`, but the detail fetch doesn't.

   **Assessment**: For the detail page this is acceptable — all properties are needed across the 17 tabs. The concern is more about the `$expand` breadth (see Part 4).

3. **`ResponseCache(Duration = 60)` on collection GET** — Client-side cache for the case list. Safe because the cache is per-client and short-lived, but be aware that newly created/forwarded cases won't appear for up to 60 seconds without a hard refresh.

4. **No `$select` pushed to `IncludeAllNavigations`** — The `[EnableQuery]` attribute can theoretically push `$select` down through EF includes, but `IncludeAllNavigations()` is called explicitly before OData middleware processes the query, so the full object graph is materialized regardless.
