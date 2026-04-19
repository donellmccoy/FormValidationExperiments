# Controller & Client Service — Microsoft Best Practices Evaluation

> **Scope:** All API controllers (`ECTSystem.Api/Controllers/`) and their corresponding Blazor WASM client services (`ECTSystem.Web/Services/`).  
> **Framework:** ASP.NET Core OData + Blazor WebAssembly with Microsoft.OData.Client  
> **References:** Microsoft REST API Guidelines, ASP.NET Core Performance Best Practices, EF Core Best Practices, OData Best Practices, Blazor WASM guidance.

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [API Controllers](#2-api-controllers)
   - [ODataControllerBase](#21-odatacontrollerbase)
   - [CasesController](#22-casescontroller)
   - [DocumentsController](#23-documentscontroller)
   - [BookmarksController](#24-bookmarkscontroller)
   - [AuthoritiesController](#25-authoritiescontroller)
   - [MembersController](#26-memberscontroller)
   - [WorkflowStateHistoryController](#27-workflowstatehistorycontroller)
   - [CaseDialogueCommentsController](#28-casedialoguecommentscontroller)
   - [UserController](#29-usercontroller)
3. [Client Services](#3-client-services)
   - [ODataServiceBase](#31-odataservicebase)
   - [EctODataContext](#32-ectoDatacontext)
   - [CaseService](#33-caseservice)
   - [AuthorityService](#34-authorityservice)
   - [BookmarkService](#35-bookmarkservice)
   - [DocumentService](#36-documentservice)
   - [MemberService](#37-memberservice)
   - [WorkflowHistoryService](#38-workflowhistoryservice)
   - [CaseDialogueService](#39-casedialogueservice)
   - [AuthService](#310-authservice)
   - [UserService](#311-userservice)
   - [CurrentUserService](#312-currentuserservice)
   - [BookmarkCountService](#313-bookmarkcountservice)
4. [Cross-Cutting Concerns](#4-cross-cutting-concerns)
   - [4.7 OData vs. ASP.NET Core Serialization Pipeline](#47-odata-vs-aspnet-core-serialization-pipeline)
5. [Summary Matrix](#5-summary-matrix)
6. [Prioritized Recommendations](#6-prioritized-recommendations)

---

## 1. Executive Summary

The codebase demonstrates strong adherence to many Microsoft best practices: pooled `IDbContextFactory`, OData query limits, `CancellationToken` propagation, ETag-based conditional requests, structured logging with `LoggerMessage`, security response headers, and proper file upload validation with magic-byte checking. The overall architecture is well-structured with clear separation of concerns.

**Key areas for improvement:**

| Priority | Category | Issue |
|----------|----------|-------|
| 🔴 High | Security | Password policy is overly relaxed for a military application |
| 🔴 High | Performance | `WorkflowHistoryService.AddHistoryEntriesAsync` uses N+1 sequential POSTs instead of OData batch |
| 🔴 High | Reliability | Several client services swallow exceptions silently with no logging |
| 🟡 Medium | API Design | Inconsistent error response format across controllers (mix of `Problem()` and raw status codes) |
| 🟡 Medium | Performance | `CaseService.SaveCaseAsync` navigation property detach/restore pattern is fragile |
| 🟡 Medium | Security | Token storage in `localStorage` is vulnerable to XSS |
| 🔴 High | Serialization | Three distinct serialization pipelines (OData / MVC JSON / Minimal API JSON) with different property naming and enum handling — no unified configuration |
| ⚠️ Medium | Serialization | DI-registered `JsonSerializerOptions` on client is never consumed; `ODataServiceBase.JsonOptions` static field used instead |
| 🟢 Low | Consistency | Dual OData client + HttpClient pattern in services adds cognitive overhead |
| 🟢 Low | Observability | Client services lack structured logging/telemetry |

---

## 2. API Controllers

### 2.1 ODataControllerBase

**File:** `ECTSystem.Api/Controllers/ODataControllerBase.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| DbContext Lifetime | ✅ **Correct** | Uses `IDbContextFactory` + `Response.RegisterForDispose()` per Microsoft guidance for OData controllers that return `IQueryable`. |
| Constructor Injection | ✅ **Correct** | All dependencies injected via constructor. |
| CancellationToken | ✅ **Correct** | `CreateContextAsync` accepts `CancellationToken`. |
| Auth Helper | ✅ **Correct** | `GetAuthenticatedUserId()` delegates to extension method, throws `UnauthorizedAccessException` on missing claim (caught by middleware). |

**Findings:**

- ✅ The `RegisterForDispose` pattern is the correct approach for OData controllers that return `IQueryable` — the context must outlive the action method for serialization. This follows the [EF Core DbContext lifetime guidance](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#using-a-dbcontext-factory-eg-for-blazor).
- ⚠️ **Protected mutable fields** — `LoggingService`, `ContextFactory`, `TimeProvider` are `protected readonly` fields, not properties. While functional, properties are the C# convention for exposing members to derived classes. Low priority.

---

### 2.2 CasesController

**File:** `ECTSystem.Api/Controllers/CasesController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Query Limits | ✅ **Correct** | `MaxTop=100`, `PageSize=50`, `MaxExpansionDepth=3`, `MaxNodeCount=500` prevents abuse. |
| Conditional GET (ETag) | ✅ **Excellent** | Lightweight RowVersion-only query → Base64 ETag → `304 Not Modified`. Follows RFC 7232. |
| DTO-Based Create | ✅ **Correct** | Uses `CreateCaseDto` → `CaseDtoMapper.ToEntity()` — prevents over-posting. |
| DTO-Based Update | ✅ **Correct** | Uses `UpdateCaseDto` with `If-Match` ETag requirement — prevents lost updates. |
| Concurrency Handling | ✅ **Correct** | Catches `DbUpdateConcurrencyException` → 409 Conflict with `Problem()` response. |
| CancellationToken | ✅ **Correct** | Propagated on all async paths. |
| Split Queries | ✅ **Correct** | `AsSplitQuery()` on single-entity reads avoids cartesian explosion. |
| AsNoTracking | ✅ **Correct** | Used for read-only queries. |

**Findings:**

- ✅ **CaseId generation with retry** — Auto-generates `YYYYMMDD-XXX` format CaseId with a retry loop for `SqlException` 2601/2627 (unique constraint violation) using `UPDLOCK`. Good defensive coding.
- ✅ **Soft delete** with Admin-only authorization and `If-Match` ETag. Follows principle of least privilege.
- ⚠️ **Missing `[ResponseCache(NoStore = true)]`** on GET collection — unlike `BookmarksController` and `DocumentsController` which correctly include it. Sensitive case data should not be cached by intermediaries.
- ⚠️ **Custom header `X-Case-IsBookmarked`** — While functional (and properly exposed via CORS `WithExposedHeaders`), this couples a user-specific concern to the entity GET response. Consider a separate `/odata/Cases({key})/IsBookmarked` function or return it in the `@odata.metadata` annotation.
- ⚠️ **CaseId retry loop** — The loop retries up to 10 times on unique constraint violation but lacks exponential backoff. Under high concurrency, all retries may collide.
- ℹ️ **Navigation property endpoints** (e.g., `GetDocuments`, `GetAuthorities`) — These are properly implemented as standard OData navigation property routes.

---

### 2.3 DocumentsController

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| File Upload Security | ✅ **Excellent** | Extension allowlist, size limit (10 MB), magic-byte signature validation, MIME mapping. |
| Request Size Limit | ✅ **Correct** | `[RequestSizeLimit(50_000_000)]` on upload action. |
| Blob Cleanup | ✅ **Correct** | Best-effort blob deletion on document delete. |
| Transaction Usage | ✅ **Correct** | `ExecutionStrategy` + explicit transaction wrapping DB insert + blob upload for consistency. |
| ResponseCache | ✅ **Correct** | `[ResponseCache(NoStore = true)]` on GET endpoints. |

**Findings:**

- ✅ **File validation is exemplary** — The combination of extension allowlist, file size check, and magic-byte signature validation is exactly what Microsoft recommends for [file upload security](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads#security-considerations).
- ⚠️ **PDF generation** (`Form348` action) — Should validate that the case exists and belongs to the user's scope before generating the PDF. Currently appears to only check existence.
- ⚠️ **Blob delete failure** — Best-effort is acceptable, but orphaned blobs should be tracked/logged for cleanup. Currently logs the error but has no retry or dead-letter mechanism.

---

### 2.4 BookmarksController

**File:** `ECTSystem.Api/Controllers/BookmarksController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| User Scoping | ✅ **Correct** | GET filters by `GetAuthenticatedUserId()`. DELETE validates ownership. |
| Idempotent POST | ✅ **Correct** | Returns existing bookmark if already bookmarked — safe for retry. |
| ResponseCache | ✅ **Correct** | `NoStore = true` on GET. |
| Model Validation | ✅ **Correct** | Checks `ModelState.IsValid` before processing. |

**Findings:**

- ✅ Clean, minimal controller. Follows the [Microsoft guidance on resource-scoped authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/resource-based).
- ℹ️ **No PATCH endpoint** — Bookmarks are create/delete only, which is appropriate for the domain.

---

### 2.5 AuthoritiesController

**File:** `ECTSystem.Api/Controllers/AuthoritiesController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData `Delta<T>.Patch()` for partial updates. |
| ResponseCache | ✅ **Correct** | `NoStore = true` on reads. |
| CancellationToken | ✅ **Correct** | Propagated. |

**Findings:**

- ⚠️ **No ownership validation** — Unlike `BookmarksController`, authorities can be created/updated/deleted without verifying the caller owns the parent case. This is a potential authorization gap. Consider resource-based authorization checking that the case belongs to the current user or the user has the appropriate role.

---

### 2.6 MembersController

**File:** `ECTSystem.Api/Controllers/MembersController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Full CRUD | ✅ **Correct** | GET, POST, PUT (via DTO), PATCH (via Delta\<T\>). |
| Navigation Property | ✅ **Correct** | `GetLineOfDutyCases()` implemented. |

**Findings:**

- ⚠️ **PUT uses full DTO** — `UpdateMemberDto` is used for PUT which is correct. However, both PUT and PATCH are available which can be confusing. Microsoft recommends choosing one or the other, with PATCH preferred for partial updates.
- ⚠️ **Missing `[ResponseCache]`** — Member data (SSN, PII) should explicitly set `NoStore = true`.

---

### 2.7 WorkflowStateHistoryController

**File:** `ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Restricted PATCH | ✅ **Good** | Only allows updating `ExitDate` — prevents tampering with history records. |

**Findings:**

- ✅ Good pattern — restricting which properties can be patched on a history/audit entity.

---

### 2.8 CaseDialogueCommentsController

**File:** `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Parent Validation | ✅ **Correct** | Validates the parent case exists before creating a comment. |
| Author-Scoped Delete | ✅ **Correct** | Only the comment author can delete their comments. |
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData Delta. |

**Findings:**

- ✅ Good resource-scoped authorization on delete.
- ⚠️ **PATCH not author-scoped** — Any authenticated user can PATCH any comment (e.g., to acknowledge it). If acknowledge is the only valid patch operation, consider a dedicated bound action instead.

---

### 2.9 UserController

**File:** `ECTSystem.Api/Controllers/UserController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Primary Constructor | ✅ **Modern** | Uses C# 12 primary constructor syntax. |
| Batch Limit | ✅ **Correct** | `Take(50)` caps the lookup batch size — prevents unbounded queries. |
| Fallback | ✅ **Correct** | Returns the user ID itself as fallback for unknown IDs. |

**Findings:**

- ✅ Clean, minimal controller. Appropriate use of `[ApiController]` (not OData) for user identity endpoints.
- ⚠️ **`GetCurrentUser` is synchronous** — Uses `User.GetRequiredUserId()` and claim lookups which are in-memory, so sync is actually fine here. No issue.
- ℹ️ **Duplicate `/me` endpoint** — `Program.cs` maps `app.MapGet("/me", ...)` and `UserController` maps `api/User/me`. The controller version returns `CurrentUserDto`; the Program.cs version returns `{ Name }`. Consider removing the Program.cs endpoint to avoid confusion.

---

## 3. Client Services

### 3.1 ODataServiceBase

**File:** `ECTSystem.Web/Services/ODataServiceBase.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Shared JsonSerializerOptions | ✅ **Correct** | `static readonly` avoids re-creation per call — follows [performance guidance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options#reuse-jsonserializeroptions-instances). |
| Generic Paged/List Helpers | ✅ **Good** | `ExecutePagedQueryAsync` and `ExecuteQueryAsync` reduce boilerplate. |
| URL Builder | ✅ **Good** | `BuildNavigationPropertyUrl` properly URL-encodes OData parameters. |

**Findings:**

- ⚠️ **Dual abstraction** — The base class holds both `EctODataContext` (typed OData client) and raw `HttpClient`. Most services use `HttpClient` for writes and `DataServiceQuery` for reads, but the split is inconsistent. This dual pattern adds cognitive overhead and increases the surface area for bugs. **Recommendation:** Pick one approach per operation type and document the convention.
- ⚠️ **No error handling in base helpers** — `ExecutePagedQueryAsync` and `ExecuteQueryAsync` do not handle `DataServiceQueryException` or network errors. Each caller must handle these individually, leading to inconsistency.
- ⚠️ **`ODataCountResponse<T>`** — Custom deserialization classes for raw `HttpClient` calls duplicate what the OData client already provides. This is a workaround for cases where the OData client doesn't support certain operations well (e.g., bound actions).

---

### 3.2 EctODataContext

**File:** `ECTSystem.Web/Services/EctODataContext.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Protocol Version | ✅ **Correct** | `ODataProtocolVersion.V4`. |
| Merge Option | ✅ **Correct** | `OverwriteChanges` ensures fresh data. |
| Entity Resolution | ✅ **Correct** | `ResolveName`/`ResolveType` properly map CLR types to OData entity sets. |

**Findings:**

- ✅ Well-structured OData client context.
- ⚠️ **Static entity-set/type maps** — `ResolveEntitySetName` and `ResolveEntityType` are `static readonly Dictionary`. If a new entity is added and the mapping is forgotten, the OData client will silently fail. Consider generating these maps or adding a startup validation check.

---

### 3.3 CaseService

**File:** `ECTSystem.Web/Services/CaseService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Query Building | ✅ **Good** | Uses `AddQueryOption` for $filter, $top, $skip, $count, $orderby, $expand. |
| ETag Handling | ✅ **Correct** | Sends `If-Match` header with RowVersion for PATCH. |
| Custom Header Extraction | ✅ **Clever** | Hooks `ReceivingResponse` event to read `X-Case-IsBookmarked`, proper cleanup in `finally`. |
| CancellationToken | ✅ **Correct** | Propagated on all paths. |

**Findings:**

- 🔴 **Navigation property detach/restore pattern** — `SaveCaseAsync` captures navigation properties (`Member`, `Documents`, `Authorities`, etc.), nulls them before PATCH, sends the request, then restores them. This is fragile and error-prone:
  - If a new navigation property is added to `LineOfDutyCase`, the developer must remember to add it here.
  - The `finally` block restores even on failure, which may leave the client with stale data.
  - **Recommendation:** Use a `JsonTypeInfoResolver` modifier (as the Program.cs comment mentions) to exclude navigation properties at serialization time rather than mutating the entity.
- ⚠️ **`GetCasesByCurrentStateAsync`** — Uses raw `HttpClient.PostAsJsonAsync` bypassing the OData client entirely. This is necessary for bound collection actions but means error handling is different from OData client calls.
- ⚠️ **Error handling inconsistency** — `CheckOutCaseAsync`/`CheckInCaseAsync` swallow `HttpRequestException` and return `false`, while `SaveCaseAsync` lets exceptions propagate. The caller has no way to distinguish "network error" from "conflict" on checkout.

---

### 3.4 AuthorityService

**File:** `ECTSystem.Web/Services/AuthorityService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Client-Side Diff | ⚠️ **Complex** | Queries existing, detaches, diffs by role, patches existing, posts new, deletes removed. |

**Findings:**

- ⚠️ **Complex client-side orchestration** — The `SaveAuthoritiesAsync` method implements a full diff/upsert algorithm on the client:
  1. Queries existing authorities
  2. Detaches them from OData context
  3. Deletes removed, patches existing (by role match), posts new
  
  This logic would be better as a server-side action (e.g., `POST /odata/Cases({key})/SaveAuthorities`) that accepts the full list and handles the diff atomically. Current approach:
  - Is **not atomic** — if one PATCH fails, the state is partially updated.
  - Generates **N+1 HTTP calls** (1 GET + N writes).
  - Client bears **business logic** that should be server-side.

---

### 3.5 BookmarkService

**File:** `ECTSystem.Web/Services/BookmarkService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Function Query | ✅ **Correct** | Uses `CreateFunctionQuery` for the `Bookmarked` bound function. |
| Graceful 404 Handling | ✅ **Good** | `RemoveBookmarkAsync` handles 404 gracefully (bookmark already deleted). |
| Batch Lookup | ✅ **Good** | `GetBookmarkedCaseIdsAsync` uses `in` filter for batch lookup. |

**Findings:**

- ⚠️ **Two-step `GetBookmarkedCasesByCurrentStateAsync`** — Gets bookmarked IDs first, then calls `ByCurrentState` with a combined filter. This is two HTTP round-trips where a single server-side action accepting both bookmark + state filters would be more efficient.
- ⚠️ **`IsBookmarkedAsync`** — Makes a full query to check existence. Could use `$top=1&$count=true` or rely on the `X-Case-IsBookmarked` header already returned by the Cases GET endpoint.

---

### 3.6 DocumentService

**File:** `ECTSystem.Web/Services/DocumentService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Explicit $select | ✅ **Excellent** | Uses `$select` to avoid over-fetching document metadata. |
| Multipart Upload | ✅ **Correct** | Uses `MultipartFormDataContent` with `StreamContent`. |
| CancellationToken | ✅ **Correct** | Propagated. |

**Findings:**

- ✅ Good use of `$select` — follows the [OData performance guidance](https://learn.microsoft.com/en-us/odata/performance/odata-query-performance).
- ⚠️ **`GetDocumentDownloadUrl`** — Returns a string URL for browser download. This URL presumably requires authentication. Ensure the token is included or the endpoint supports query-string token fallback for direct browser downloads.

---

### 3.7 MemberService

**File:** `ECTSystem.Web/Services/MemberService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Search Logic | ⚠️ **Complex Client-Side** | Builds complex OData `$filter` with `contains()` on multiple fields, rank-to-paygrade dictionary, enum matching with regex. |
| Result Limiting | ✅ **Good** | `$top=25` limits results. |

**Findings:**

- ⚠️ **Complex filter construction on client** — The search builds a large OData filter string with `contains()` on `FirstName`, `LastName`, `SSN`, `MemberID`, pay grade matching, and component enum matching. This filter logic should ideally live on the server as a dedicated search endpoint (e.g., `POST /odata/Members/Search`) that accepts a simple search term and builds the query server-side. Benefits:
  - Simpler client code
  - Server can optimize (full-text search, indexed columns)
  - Filter syntax changes don't require client updates
- ⚠️ **Regex-based enum display name matching** — Splitting `ServiceComponent` display names with regex to match user input is fragile and locale-dependent.

---

### 3.8 WorkflowHistoryService

**File:** `ECTSystem.Web/Services/WorkflowHistoryService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Paged Query | ✅ **Good** | Uses `ExecutePagedQueryAsync` for list operations. |
| CancellationToken | ✅ **Correct** | Propagated on most methods. |

**Findings:**

- 🔴 **N+1 sequential POST in `AddHistoryEntriesAsync`** — Loops through entries and calls `PostAsJsonAsync` sequentially for each one. For N entries, this makes N HTTP round-trips. **Recommendation:** Use OData batch (`$batch`) to send all entries in a single request. The API already registers `DefaultODataBatchHandler` and calls `app.UseODataBatching()`, but the client never uses it.
- ⚠️ **`UpdateHistoryEndDateAsync`** — Manually constructs a PATCH request with an anonymous object. Uses `DateTime.UtcNow` on the client side for `ExitDate`, which means the timestamp comes from the user's clock rather than the server's `TimeProvider`. This contradicts the server-side pattern of using injected `TimeProvider` for consistent UTC timestamps.

---

### 3.9 CaseDialogueService

**File:** `ECTSystem.Web/Services/CaseDialogueService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| CancellationToken | ✅ **Correct** | All methods accept and propagate `CancellationToken`. |
| Paged Query | ✅ **Good** | Uses `BuildNavigationPropertyUrl` with proper pagination parameters. |

**Findings:**

- ⚠️ **`AcknowledgeAsync` uses `DateTime.UtcNow`** — Same issue as `WorkflowHistoryService`: client-side timestamp instead of server-side `TimeProvider`. The server should compute the acknowledgment timestamp.
- ⚠️ **Manual PATCH construction** — Creates `HttpRequestMessage` with `HttpMethod.Patch` and `JsonContent.Create`. This is correct but verbose compared to using a helper method.

---

### 3.10 AuthService

**File:** `ECTSystem.Web/Services/AuthService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Token Storage | ⚠️ **Risk** | Stores access/refresh tokens in `localStorage` via Blazored.LocalStorage. |
| Error Handling | ✅ **Good** | Returns structured `AuthResult` with error messages on failure. |
| Logout | ✅ **Correct** | Clears both tokens + cached user state. |

**Findings:**

- 🟡 **`localStorage` token storage** — `localStorage` is accessible to any JavaScript running on the page, making tokens vulnerable to XSS attacks. Microsoft recommends `httpOnly` cookies for token storage in production. However, in Blazor WASM (client-side), JavaScript interop is required for cookie management too, so this is a common trade-off. **Mitigation:** Ensure robust CSP headers (already present in `Program.cs`) and sanitize all user inputs.
- ✅ **`NotifyAuthenticationStateChanged`** — Correctly triggers Blazor auth state refresh after login/logout.
- ⚠️ **No token refresh** — `RefreshToken` is stored but there's no automatic token refresh mechanism (e.g., `DelegatingHandler` that intercepts 401s and retries with the refresh token).

---

### 3.11 UserService

**File:** `ECTSystem.Web/Services/UserService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Client-Side Cache | ✅ **Good** | Dictionary cache avoids redundant lookups for known user IDs. |
| Batch Lookup | ✅ **Correct** | Batches uncached IDs into a single HTTP request. |
| Primary Constructor | ✅ **Modern** | C# 12 syntax. |

**Findings:**

- ⚠️ **Cache never expires** — The `_cache` `Dictionary<string, string>` grows indefinitely and never clears. In a long-running Blazor WASM session, this could accumulate stale data (e.g., user changed their display name). Consider a TTL-based cache or clear on navigation.
- ⚠️ **No CancellationToken on `GetDisplayNameAsync`** — The overload passes it through, but the method signature should also accept a default `CancellationToken` parameter. *(Actually it does — good.)*
- ✅ Proper URL encoding with `Uri.EscapeDataString`.

---

### 3.12 CurrentUserService

**File:** `ECTSystem.Web/Services/CurrentUserService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Lazy Initialization | ✅ **Good** | Fetches user ID on first access, caches thereafter. |
| IHttpClientFactory | ✅ **Correct** | Uses named client `"Api"` — follows [Microsoft guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests). |
| Logout Cleanup | ✅ **Correct** | `Clear()` method resets cached state. |

**Findings:**

- ✅ Well-designed for the opaque Data Protection token scenario where user ID can't be extracted client-side.
- ⚠️ **Race condition** — If two components call `GetUserIdAsync()` simultaneously before the cache is populated, two HTTP requests may be sent. Use `SemaphoreSlim` or `Lazy<Task<T>>` for thread-safe lazy initialization.

---

### 3.13 BookmarkCountService

**File:** `ECTSystem.Web/Services/BookmarkCountService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Event-Based Notification | ✅ **Good** | `OnCountChanged` event pattern allows decoupled UI updates. |
| Optimized Server Query | ✅ **Excellent** | Uses `$top=0&$count=true` to fetch only the count. |
| Graceful Failure | ✅ **Acceptable** | Silently swallows errors — appropriate for a non-critical badge metric. |

**Findings:**

- ⚠️ **`Increment()`/`Decrement()` are optimistic** — They adjust the count without verifying the server operation succeeded. If the `AddBookmarkAsync` call fails after the `Increment()`, the badge count will be wrong until the next `RefreshAsync()`. Consider calling `Increment()`/`Decrement()` only after the server operation confirms success.
- ⚠️ **Bare `catch`** — The `catch` block swallows all exceptions including `OutOfMemoryException`. Use `catch (Exception)` at minimum, or better, catch specific HTTP/network exceptions.

---

## 4. Cross-Cutting Concerns

### 4.1 Security

| Aspect | Status | Details |
|--------|--------|---------|
| Authentication | ✅ | All controllers use `[Authorize]`. Identity API endpoints properly mapped. |
| Authorization Policies | ✅ | `Admin`, `CaseManager`, `CanManageDocuments` policies defined. |
| Security Headers | ✅ | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `CSP` all set. |
| CORS | ✅ | Origin allowlist, exposed headers, `AllowCredentials()`. |
| File Upload | ✅ | Extension allowlist + magic bytes + size limit. |
| PII Scrubbing | ✅ | SSN patterns scrubbed from request/response logs. |
| Password Policy | 🔴 | `RequireDigit=false`, `RequireUppercase=false`, `RequireNonAlphanumeric=false`, `RequiredLength=6`. For a military LOD system handling PII, this is too weak. Increase to minimum 12 characters with complexity requirements per DoD guidance. |
| Rate Limiting | ⚠️ | Defined but commented out (`AddApiRateLimiting`, `UseRateLimiter`). Should be enabled for production. |
| HTTPS Redirection | ✅ | `UseHttpsRedirection()` in pipeline. |

### 4.2 Error Handling & Problem Details

| Aspect | Status | Details |
|--------|--------|---------|
| Global Exception Handler | ✅ | Production: `UseExceptionHandler` with ProblemDetails-style JSON. Dev: `UseDeveloperExceptionPage`. |
| OperationCancelled | ✅ | Dedicated middleware returns 499 instead of 500 for client disconnects. |
| UnauthorizedAccess | ✅ | Dedicated middleware returns 401 with RFC 7235 format. |
| Controller Error Format | ⚠️ | Mix of `Problem()`, `ValidationProblem()`, raw `StatusCode()`, and `NotFound()`. Should consistently use `Problem()` for RFC 7807 compliance. |
| Client Error Handling | ⚠️ | Inconsistent — some services throw, some return `null`/`false`, some swallow exceptions. Need a unified error-handling strategy. |

### 4.3 Performance

| Aspect | Status | Details |
|--------|--------|---------|
| DbContext Pooling | ✅ | `AddPooledDbContextFactory` with `poolSize: 32`. |
| Query Splitting | ✅ | Global default `SplitQuery` + explicit `AsSplitQuery()` on key reads. |
| Retry on Failure | ✅ | SQL connection retry with `EnableRetryOnFailure`. |
| OData Batch (Server) | ✅ | `DefaultODataBatchHandler` registered, `UseODataBatching()` in pipeline. |
| OData Batch (Client) | 🔴 | **Never used.** Client sends individual HTTP requests even for batch operations. |
| $select Usage | ✅ | `DocumentService` uses `$select` to reduce payload. Other services could benefit. |
| Streaming | ✅ | Document download uses `OpenReadAsync()` for streaming. |

### 4.4 Structured Logging

| Aspect | Status | Details |
|--------|--------|---------|
| Server Logging | ✅ | `LoggerMessage` source generators via `ILoggingService` — high-performance structured logging. |
| Request Logging | ✅ | `RequestLoggingMiddleware` with timing, method, path, status code. Body logging in dev only. |
| Client Logging | 🔴 | **No logging in client services.** Failed HTTP calls are either thrown or silently swallowed with no diagnostic information. |

### 4.5 Middleware Pipeline Order

```
UseODataBatching()        ← Before routing (correct)
RequestLoggingMiddleware  ← Measures full request time
OperationCancelledMiddleware
UnauthorizedAccessMiddleware
UseHttpsRedirection()
UseCors()
UseAuthentication()       ← Before Authorization (correct)
UseAuthorization()
MapIdentityApi()
MapControllers()
```

✅ Pipeline order follows [Microsoft ASP.NET Core middleware documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/#middleware-order).

### 4.6 Async Best Practices

| Aspect | Status | Details |
|--------|--------|---------|
| `async Task` return types | ✅ | All async methods return `Task` or `Task<T>`, not `async void`. |
| `CancellationToken` propagation | ✅ | Consistent on server. Client services mostly propagate but with some gaps (noted per service). |
| `ConfigureAwait` | ✅ | Not needed in ASP.NET Core (no `SynchronizationContext`). Not needed in Blazor WASM (single-threaded). |
| No sync-over-async | ✅ | No `.Result` or `.Wait()` calls observed. |

### 4.7 OData vs. ASP.NET Core Serialization Pipeline

The application has **three distinct serialization pipelines on the server** and **three on the client**, creating significant risk for mismatches in property naming, enum formatting, date handling, and null semantics.

#### Server-Side Pipelines

| Pipeline | Endpoints Affected | Configuration | Property Naming | Enum Handling |
|----------|--------------------|---------------|-----------------|---------------|
| **OData Serializer** | All 8 OData controllers (`Cases`, `Documents`, `Bookmarks`, `Authorities`, `Members`, `WorkflowHistory`, `CaseDialogueComments`, `Notifications`) | Driven by EDM model via `BuildEdmModel()` in `ServiceCollectionExtensions.cs` | PascalCase (follows CLR property names in EDM) | String names by default (EDM enum type definitions) |
| **MVC JSON** (`AddJsonOptions`) | `UserController` | `JsonStringEnumConverter` + `ReferenceHandler.IgnoreCycles` (lines 107-120, API `ServiceCollectionExtensions.cs`) | PascalCase (default MVC) | String via `JsonStringEnumConverter` |
| **Minimal API JSON** | `MapIdentityApi<ApplicationUser>()` endpoints (`/login`, `/register`, `/refresh`, `/confirmEmail`, etc.) + `MapGet("/me", ...)` | **No explicit configuration** — `AddJsonOptions` on `AddControllers()` does **not** affect minimal API endpoints | camelCase (minimal API default uses `JsonSerializerDefaults.Web`) | Integer (no `JsonStringEnumConverter` configured for minimal APIs) |

**Key server-side finding:** `AddControllers().AddJsonOptions()` only configures the MVC serializer. Minimal API endpoints (Identity API, `/me`) use their own `JsonSerializerOptions` — which defaults to `JsonSerializerDefaults.Web` (camelCase, case-insensitive). To configure minimal API JSON, the app must use `builder.Services.ConfigureHttpJsonOptions()`.

#### Client-Side Pipelines

| Pipeline | Used By | Configuration | Property Naming | Enum Handling | Cycle Handling |
|----------|---------|---------------|-----------------|---------------|----------------|
| **OData Client** (`Microsoft.OData.Client`) | `ExecutePagedQueryAsync`, `ExecuteQueryAsync`, all OData reads | EDM model from `BuildClientEdmModel()` | Follows EDM model (PascalCase) | EDM enum type definitions | N/A (OData protocol) |
| **`ODataServiceBase.JsonOptions`** (static) | `CaseService` (writes), `BookmarkService`, `WorkflowHistoryService`, `CaseDialogueService`, `MemberService`, `UserService`, `CurrentUserService` via `HttpClient` | `PropertyNameCaseInsensitive = true`, `PropertyNamingPolicy = null`, `JsonStringEnumConverter` | **PascalCase** (null policy) | String via converter | **None** — no `ReferenceHandler` |
| **Default (no explicit options)** | `AuthService` — `PostAsJsonAsync()` / `ReadFromJsonAsync()` without options | Defaults to `JsonSerializerDefaults.Web` | **camelCase** | **Integer** (no converter) | None |
| **DI-registered singleton** | **⚠️ Not injected by any service** | `JsonSerializerDefaults.Web` + `JsonStringEnumConverter` + `ReferenceHandler.IgnoreCycles` | camelCase (Web defaults) | String via converter | `IgnoreCycles` |

#### Specific Mismatches

| # | Mismatch | Risk | Severity |
|---|----------|------|----------|
| 1 | **Minimal API endpoints serialize camelCase; MVC endpoints serialize PascalCase** | Any client code calling both Identity endpoints and UserController expects different property casing from the same API. The `/me` minimal-API endpoint and `UserController.GetCurrentUser` return the same data in different casing. | 🔴 High |
| 2 | **AuthService sends/receives camelCase but API Identity endpoints also use camelCase** (by luck, not design) | Currently works, but is fragile. If `ConfigureHttpJsonOptions()` is added later for minimal APIs or `AuthService` switches to explicit options, the match could break. | ⚠️ Medium |
| 3 | **DI-registered `JsonSerializerOptions` is never consumed** | Wasted registration. The DI singleton has `ReferenceHandler.IgnoreCycles` + `JsonStringEnumConverter` — the intended "correct" configuration — but no service injects it. | ⚠️ Medium |
| 4 | **`ODataServiceBase.JsonOptions` lacks `ReferenceHandler.IgnoreCycles`** | If an OData endpoint returns a response with reference cycles (e.g., parent ↔ child navigation properties) and the client uses `HttpClient` with `ODataServiceBase.JsonOptions` instead of the OData client, deserialization will throw. | ⚠️ Medium |
| 5 | **OData JSON response metadata vs. raw JSON** | `ODataCountResponse<T>` and `ODataResponse<T>` in `ODataServiceBase` use `[JsonPropertyName("@odata.count")]` and `[JsonPropertyName("value")]` to manually parse OData JSON envelope via `System.Text.Json`. This works but tightly couples the client to OData's JSON format and bypasses the type-safe OData client. | 🟡 Low |
| 6 | **Mixed deserialization paths for same entity types** | Services like `CaseService` use the OData client for reads (`ExecutePagedQueryAsync`) and `HttpClient` + `System.Text.Json` for writes (`PostAsJsonAsync` + `ReadFromJsonAsync`). The same `LineOfDutyCase` entity is deserialized by two different engines with different configurations. | ⚠️ Medium |

#### Recommendations

1. **Consolidate to a single `JsonSerializerOptions`:** Inject the DI-registered singleton into `ODataServiceBase` (via constructor) instead of using the static `JsonOptions` field. This ensures all `HttpClient`-based operations use the same configuration.

2. **Configure minimal API JSON:** Add `builder.Services.ConfigureHttpJsonOptions(options => { ... })` in `Program.cs` with the same `JsonStringEnumConverter` and `ReferenceHandler.IgnoreCycles` to align minimal API serialization with MVC controllers.

3. **Fix AuthService:** Pass explicit `JsonSerializerOptions` to all `PostAsJsonAsync` / `ReadFromJsonAsync` calls, or inject the DI-registered options.

4. **Add `ReferenceHandler.IgnoreCycles`** to whichever `JsonSerializerOptions` survives consolidation.

5. **Remove the duplicate `/me` minimal-API endpoint** — the `UserController.GetCurrentUser` MVC endpoint is the authoritative version (already noted in recommendation #12).

6. **Add serialization round-trip integration tests** — verify that entities serialized by the API (both OData and non-OData endpoints) can be deserialized by the client's `JsonSerializerOptions`, and vice versa.

---

## 5. Summary Matrix

| Component | Security | Performance | Error Handling | API Design | Logging | Overall |
|-----------|----------|-------------|----------------|------------|---------|---------|
| ODataControllerBase | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| CasesController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| DocumentsController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| BookmarksController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| AuthoritiesController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| MembersController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| WorkflowStateHistoryController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| CaseDialogueCommentsController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ |
| UserController | ✅ | ✅ | ✅ | ✅ | N/A | ✅ |
| ODataServiceBase | N/A | ⚠️ | ⚠️ | ⚠️ | 🔴 | ⚠️ |
| CaseService | ✅ | ⚠️ | ⚠️ | ⚠️ | 🔴 | ⚠️ |
| AuthorityService | ✅ | 🔴 | ⚠️ | ⚠️ | 🔴 | ⚠️ |
| BookmarkService | ✅ | ⚠️ | ✅ | ✅ | 🔴 | ⚠️ |
| DocumentService | ✅ | ✅ | ✅ | ✅ | 🔴 | ✅ |
| MemberService | ✅ | ✅ | ⚠️ | ⚠️ | 🔴 | ⚠️ |
| WorkflowHistoryService | ⚠️ | 🔴 | ⚠️ | ⚠️ | 🔴 | ⚠️ |
| CaseDialogueService | ⚠️ | ✅ | ✅ | ✅ | 🔴 | ⚠️ |
| AuthService | ⚠️ | ✅ | ✅ | ✅ | 🔴 | ⚠️ |
| UserService | ✅ | ⚠️ | ⚠️ | ✅ | 🔴 | ⚠️ |
| CurrentUserService | ✅ | ⚠️ | ⚠️ | ✅ | 🔴 | ⚠️ |

---

## 6. Prioritized Recommendations

### 🔴 High Priority

| # | Issue | Location | Recommendation |
|---|-------|----------|----------------|
| 1 | **Password policy too weak** | `ServiceCollectionExtensions.AddIdentity()` | Increase to 12+ characters with complexity per DoD STIG requirements. |
| 2 | **N+1 HTTP calls for batch operations** | `WorkflowHistoryService.AddHistoryEntriesAsync`, `AuthorityService.SaveAuthoritiesAsync` | Use OData `$batch` requests (already configured server-side) or create server-side actions that accept collections. |
| 3 | **Client-side timestamps** | `CaseDialogueService.AcknowledgeAsync`, `WorkflowHistoryService.UpdateHistoryEndDateAsync` | Let the server set timestamps via `TimeProvider`. Send only the intent (e.g., "acknowledge") and let the controller compute the timestamp. |
| 4 | **No client-side logging** | All client services | Add `ILogger<T>` to client services. Log failed HTTP requests, OData errors, and timeout conditions at minimum. |
| 5 | **Three uncorrelated serialization pipelines** | API `ServiceCollectionExtensions.cs`, `Program.cs` | `AddControllers().AddJsonOptions()` does NOT configure minimal API endpoints (`MapIdentityApi`, `/me`). Add `ConfigureHttpJsonOptions()` to align minimal API serialization with MVC. OData, MVC, and minimal API each serialize differently (property naming, enum format). See §4.7. |
| 6 | **Client `JsonSerializerOptions` fragmentation** | `ODataServiceBase.JsonOptions`, DI singleton, `AuthService` | Consolidate to a single DI-injected `JsonSerializerOptions` used by all services. The DI singleton already has the correct config but is never consumed. `AuthService` uses implicit Web defaults (camelCase, no enum converter). See §4.7. |

### 🟡 Medium Priority

| # | Issue | Location | Recommendation |
|---|-------|----------|----------------|
| 7 | **Navigation property detach/restore** | `CaseService.SaveCaseAsync` | Replace manual null/restore with `JsonTypeInfoResolver` modifier to exclude navigation properties at serialization time. |
| 8 | **Missing resource authorization** | `AuthoritiesController`, `CaseDialogueCommentsController` PATCH | Validate that the authenticated user has permission to modify the parent case. |
| 9 | **Enable rate limiting** | `Program.cs` | Uncomment `AddApiRateLimiting()` and `UseRateLimiter()` for production. |
| 10 | **Inconsistent error responses** | Multiple controllers | Standardize on `Problem()` for all error responses (RFC 7807). |
| 11 | **Missing `ResponseCache` attribute** | `CasesController.Get` (collection), `MembersController` | Add `[ResponseCache(NoStore = true)]` to endpoints returning PII. |
| 12 | **Client error handling strategy** | Multiple client services | Establish a convention: return `Result<T>` or throw typed exceptions. Avoid swallowing errors silently or inconsistently returning `null`/`false`. |
| 13 | **Token refresh mechanism** | `AuthService` | Implement a `DelegatingHandler` that intercepts 401 responses and automatically retries with the stored refresh token before failing. |
| 14 | **Duplicate `/me` endpoint** | `Program.cs` + `UserController` | Remove the `app.MapGet("/me", ...)` in `Program.cs` — the `UserController.GetCurrentUser` is the authoritative version. |

### 🟢 Low Priority

| # | Issue | Location | Recommendation |
|---|-------|----------|----------------|
| 15 | **Move search logic server-side** | `MemberService.SearchMembersAsync` | Create a `POST /odata/Members/Search` action that accepts a search term. |
| 16 | **Consolidate bookmark state query** | `BookmarkService.GetBookmarkedCasesByCurrentStateAsync` | Create a server-side action that combines bookmark + workflow state filtering in one query. |
| 17 | **Thread-safe lazy init** | `CurrentUserService.GetUserIdAsync` | Use `SemaphoreSlim` or `Lazy<Task<T>>` to prevent duplicate HTTP requests on concurrent access. |
| 18 | **Cache TTL for UserService** | `UserService._cache` | Add time-based expiry or size limit to prevent unbounded growth in long sessions. |
| 19 | **Protected fields → properties** | `ODataControllerBase` | Change `protected readonly` fields to `protected` properties for idiomatic C#. |
| 20 | **OData client vs HttpClient convention** | `ODataServiceBase` and derived services | Document when to use each. Consider standardizing: OData client for reads, HttpClient for writes/actions. |
| 21 | **Add serialization round-trip integration tests** | `ECTSystem.Tests` | Verify that entities serialized by the API (OData, MVC, and minimal API) can be deserialized by the client with all three `JsonSerializerOptions` configurations. |
