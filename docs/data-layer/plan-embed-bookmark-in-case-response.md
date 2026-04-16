# Embed Bookmark Status in Case Response

> **Status:** ✅ Implemented — Option C (OData `ReceivingResponse` pipeline interception)

## Goal

Eliminate the separate `GET /odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {id}&$top=1` call when loading a single case on the EditCase page. Instead, return the bookmark status as part of the case response itself, saving 1 HTTP round-trip per case load.

---

## Previous Flow (2 calls)

```
EditCase.LoadCaseAsync()
  ├─ GET /odata/Cases?$filter=CaseId eq '{id}'&$expand=...  → LineOfDutyCase
  └─ GET /odata/CaseBookmarks?$filter=LineOfDutyCaseId eq {id}&$top=1&$select=Id  → bool
```

## Current Flow (1 call)

```
EditCase.LoadCaseAsync()
  └─ GET /odata/Cases?$filter=CaseId eq '{id}'&$expand=...  → LineOfDutyCase + X-Case-IsBookmarked header
```

---

## Approach: Custom Response Header

Return bookmark status as a **custom HTTP response header** on the single-case GET endpoint. This avoids polluting the OData entity model with user-specific state and keeps the entity cacheable.

### Why not an entity property?

- `IsBookmarkedByCurrentUser` is **user-specific** — two users GET the same case and get different values. Adding it to the OData entity type would:
  - Break OData caching semantics (same URL, different responses per user)
  - Require `[NotMapped]` + manual population, which the OData serializer may or may not include
  - Pollute PATCH `Delta<T>` — clients could accidentally send it back
- A header is invisible to the OData pipeline and doesn't affect entity serialization.

### Why not `$expand=Bookmarks`?

- Would require adding a back-navigation `ICollection<CaseBookmark>` to `LineOfDutyCase`, which exposes other users' bookmarks unless filtered
- OData `$expand` with `$filter` on navigation properties adds complexity and still requires the client to inspect the collection

---

## Implementation Steps

### Step 1: API — Add header to single-case GET endpoint ✅

**File:** `ECTSystem.Api/Controllers/CasesController.cs`

In the `Get([FromODataUri] int key)` method, after the ETag/CacheControl headers and before `return Ok(lodCase)`, query the bookmark status and set a response header:

```csharp
var isBookmarked = await context.CaseBookmarks
    .AnyAsync(b => b.UserId == UserId && b.LineOfDutyCaseId == key, ct);

Response.Headers["X-Case-IsBookmarked"] = isBookmarked.ToString().ToLowerInvariant();
```

**Notes:**
- Uses the existing `UserId` property (from `ODataControllerBase`) and the existing `context` variable (`IDbContextFactory`).
- The `AnyAsync` query hits the existing unique index on `(UserId, LineOfDutyCaseId)` — essentially free.
- Header value is `"true"` or `"false"`.
- Emitted for all requests (the `UserId` claim is always present via `[Authorize]`).

### Step 2: API — Expose the header to the browser via CORS ✅

**File:** `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs`

Added `"X-Case-IsBookmarked"` to the `"BlazorClient"` CORS policy via `.WithExposedHeaders(...)`. Without this, the browser's fetch API does not surface custom headers to Blazor WASM.

```csharp
policy.WithOrigins(...)
      .AllowAnyHeader()
      .AllowAnyMethod()
      .AllowCredentials()
      .WithExposedHeaders("X-Case-IsBookmarked");
```

### Step 3: Web — Read the header via OData pipeline interception (Option C) ✅

**File:** `ECTSystem.Web/Services/CaseHttpService.cs`

Uses `Context.ReceivingResponse` event on `EctODataContext` (inherited from `DataServiceContext`) to capture the custom response header. A local function handler is subscribed before the OData query and unsubscribed in a `finally` block to prevent leaking event handlers:

```csharp
public async Task<(LineOfDutyCase? Case, bool? IsBookmarked)> GetCaseAsync(
    string caseId, CancellationToken cancellationToken = default)
{
    bool? isBookmarked = null;

    void OnReceivingResponse(object? sender, ReceivingResponseEventArgs args)
    {
        var headerValue = args.ResponseMessage?.GetHeader("X-Case-IsBookmarked");
        if (!string.IsNullOrEmpty(headerValue) && bool.TryParse(headerValue, out var val))
        {
            isBookmarked = val;
        }
    }

    Context.ReceivingResponse += OnReceivingResponse;

    try
    {
        var query = Context.Cases
            .AddQueryOption("$filter", $"CaseId eq '{caseId}'")
            .AddQueryOption("$top", 1)
            .AddQueryOption("$expand", FullExpand);

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return (results.FirstOrDefault(), isBookmarked);
    }
    finally
    {
        Context.ReceivingResponse -= OnReceivingResponse;
    }
}
```

**Why Option C over Option A:**
- Preserves the typed OData query pattern — no manual URL construction or raw `HttpClient` deserialization.
- The closure variable `isBookmarked` is scoped to the method call, avoiding thread-safety issues.
- Subscribe/unsubscribe pattern prevents the event handler from firing on unrelated OData calls.

### Step 4: Web — Update ICaseService interface ✅

**File:** `ECTSystem.Web/Services/ICaseService.cs`

Updated the return type:

```csharp
Task<(LineOfDutyCase? Case, bool? IsBookmarked)> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);
```

### Step 5: Web — Use embedded status in EditCase ✅

**File:** `ECTSystem.Web/Pages/EditCase.razor.cs`

In `LoadCaseAsync`, destructures the tuple and uses the embedded bookmark status when present, with a fallback to `CheckBookmarkAsync()` for resilience:

```csharp
var (lodCase, isBookmarked) = await CaseService.GetCaseAsync(CaseId, _cts.Token);
_lineOfDutyCase = lodCase;

// ... null checks and setup ...

if (isBookmarked.HasValue)
{
    _bookmark.IsBookmarked = isBookmarked.Value;
}
else
{
    _bookmark.IsBookmarked = await CheckBookmarkAsync();
}

await previousCasesTask;
```

### Step 6: Existing BookmarkService unchanged ✅

`AddBookmarkAsync`, `RemoveBookmarkAsync`, `IsBookmarkedAsync`, and `GetBookmarkedCaseIdsAsync` remain unchanged. They are still needed for:
- Toggling bookmarks (POST/DELETE)
- Batch bookmark checks on the previous-cases grid
- Fallback if the header is absent

---

## Files Changed

| File | Change |
|------|--------|
| `ECTSystem.Api/Controllers/CasesController.cs` | Add `X-Case-IsBookmarked` header to single-case GET |
| `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` | Expose `X-Case-IsBookmarked` in CORS `WithExposedHeaders` |
| `ECTSystem.Web/Services/ICaseService.cs` | Update `GetCaseAsync` return type to `(LineOfDutyCase? Case, bool? IsBookmarked)` |
| `ECTSystem.Web/Services/CaseHttpService.cs` | Read bookmark status via `ReceivingResponse` pipeline interception |
| `ECTSystem.Web/Pages/EditCase.razor.cs` | Use embedded bookmark status with `CheckBookmarkAsync` fallback |

## Files NOT Changed

| File | Reason |
|------|--------|
| `ECTSystem.Shared/Models/LineOfDutyCase.cs` | No model changes — bookmark status travels via header |
| `ECTSystem.Shared/Models/CaseBookmark.cs` | No model changes |
| `ECTSystem.Web/Services/BookmarkHttpService.cs` | Still needed for toggle (POST/DELETE) and batch checks |
| `ECTSystem.Api/Controllers/CaseBookmarksController.cs` | Still needed for POST/DELETE/batch operations |
| OData EDM configuration | No EDM changes |

---

## Risk Assessment

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| CORS blocks custom header | Medium | Step 2 `WithExposedHeaders` is required; test in browser DevTools |
| OData client swallows header | Low | Option A (raw HttpClient) avoids this entirely; Option C (ReceivingResponse) also works |
| Cache returns stale bookmark status | Low | Single-case GET already uses `must-revalidate` and ETag; no shared CDN cache |
| Breaking change to ICaseService | Low | Tuple return is additive; `bool?` null = "not loaded" preserves backward compat |

## Testing

1. **API unit test**: Verify `X-Case-IsBookmarked` header is `"true"` when bookmark exists, `"false"` when it doesn't, absent when unauthenticated.
2. **Integration test**: Hit `GET /odata/Cases({id})` with auth token, verify header is present and correct.
3. **Web manual test**: Load EditCase, verify bookmark icon is correct on first render without seeing a second `/CaseBookmarks` request in DevTools Network tab.
4. **CORS test**: Verify `Access-Control-Expose-Headers` includes `X-Case-IsBookmarked` in preflight response.
