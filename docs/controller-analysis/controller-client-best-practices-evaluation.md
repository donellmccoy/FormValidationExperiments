# Controller & Client Service — Microsoft Best Practices Evaluation

> **Scope:** All API controllers (`ECTSystem.Api/Controllers/`) and their corresponding Blazor WASM client services (`ECTSystem.Web/Services/`).  
> **Framework:** ASP.NET Core OData + Blazor WebAssembly with Microsoft.OData.Client  
> **References:** Microsoft REST API Guidelines, ASP.NET Core Performance Best Practices, EF Core Best Practices, OData Best Practices, Blazor WASM guidance.  
> **Last Re-Evaluated:** 2026-04-13 — full re-scan of all 9 controllers + 13 client services + 4 infra files. See [Re-Evaluation Delta](#11-re-evaluation-delta) for status changes since prior revision.

---

## 1.1 Re-Evaluation Delta

### ✅ Recently Resolved / Verified Correct

These items were either fixed since the last revision or re-verified as already compliant on this pass.

| Item | Evidence |
|------|----------|
| **Magic-byte file upload validation** | [ECTSystem.Api/Controllers/DocumentsController.cs](../../ECTSystem.Api/Controllers/DocumentsController.cs) — extension allowlist + signature check + 10 MB per-file / 50 MB total cap |
| **ETag / RowVersion concurrency on `CasesController`** | RowVersion preconditions, retry on SQL 2601/2627, Admin-only DELETE |
| **`BookmarksController` user-scoping** | User-bound queries, idempotent POST, ProblemDetails, `ResponseCache(NoStore = true)` |
| **`MemberService` server-side filter quoting** | [ECTSystem.Web/Services/MemberService.cs](../../ECTSystem.Web/Services/MemberService.cs) — `Replace("'", "''")` defends against OData filter injection in `contains(tolower(...))` |
| **`BookmarkService` clean batched access** | Uses bound function `Default.Bookmarked` + `ByCurrentState` action + `Id in (…)` batched filter — no N+1 |
| **HttpClient resilience** | `AddStandardResilienceHandler` registered on both `Api` and `OData` named clients ([ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs](../../ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs)) |
| **Security headers + ProblemDetails** | `Program.cs` — `X-Content-Type-Options`, `X-Frame-Options`, CSP, Referrer-Policy; `AddProblemDetails()` registered |

### 🆕 Newly Identified Issues

Discovered during this re-scan; not in prior revision.

| # | Issue | Location | Severity |
|---|-------|----------|----------|
| N1 | **`WorkflowStateHistoryController` accepts client-supplied `EntryDate` / `ExitDate`** without server override — clients can backdate or forward-date workflow transitions, undermining the audit trail. | [ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs](../../ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs) | 🔴 High |
| N2 | **Duplicate `/me` endpoints** with divergent payloads. `Program.cs` exposes `app.MapGet("/me", ...)` returning `{ Name }` (camelCase, integer enums); `UserController` exposes `GET api/User/me` returning `CurrentUserDto { UserId, Name }` (PascalCase, string enums). | [ECTSystem.Api/Program.cs](../../ECTSystem.Api/Program.cs) + [ECTSystem.Api/Controllers/UserController.cs](../../ECTSystem.Api/Controllers/UserController.cs) | 🟡 Medium |
| N3 | **`UserService` in-memory cache has no eviction.** `Dictionary<string, string> _cache = new()` grows unbounded over a session — long-lived sessions leak memory and surface stale display names. | [ECTSystem.Web/Services/UserService.cs](../../ECTSystem.Web/Services/UserService.cs) | 🟡 Medium |
| N4 | **Stale `NotificationsController` reference in §4.7.** No `NotificationsController` exists in `ECTSystem.Api/Controllers/`, yet the `Notification` entity, server `EntitySet<Notification>`, client `EdmModel`, `EctODataContext.Notifications` query, and `CaseService.FullExpand` all reference it. Either the entity is dead code or the controller is missing. | [ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs](../../ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs), [ECTSystem.Web/Services/EctODataContext.cs](../../ECTSystem.Web/Services/EctODataContext.cs), [ECTSystem.Web/Services/CaseService.cs](../../ECTSystem.Web/Services/CaseService.cs) | 🟡 Medium |
| N5 | **`CaseDialogueCommentsController.Patch` lacks role check.** `DELETE` currently enforces "author or Admin"; `PATCH` allows any authenticated user to edit any comment. **Per current policy, only the `Admin` role should be used** — both PATCH and DELETE should be restricted to `Admin` for now. | [ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs](../../ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs) | 🔴 High |
| N6 | **`BookmarkCountService` + `CurrentUserService` use bare `catch { }`** — exceptions are swallowed without logging, making badge-count and identity-resolution failures invisible. `CurrentUserService` also lacks a `SemaphoreSlim` around lazy init, allowing duplicate `/me` calls under concurrent access. | [ECTSystem.Web/Services/BookmarkCountService.cs](../../ECTSystem.Web/Services/BookmarkCountService.cs), [ECTSystem.Web/Services/CurrentUserService.cs](../../ECTSystem.Web/Services/CurrentUserService.cs) | 🟢 Low |

### ❌ Still Outstanding (Unchanged from Prior Revision)

- Weak Identity password policy (`RequiredLength=6`, no complexity)
- Rate limiting registered but commented out in pipeline
- `WorkflowHistoryService.AddHistoryEntriesAsync` sequential POST loop (N+1)
- `AuthorityService.SaveAuthoritiesAsync` sequential DELETE/PATCH/POST per role (N+1)
- `CaseService.SaveCaseAsync` 10-navigation detach-and-restore pattern around raw PATCH
- Three uncorrelated `JsonSerializerOptions` pipelines on client; DI singleton dead
- `CaseDialogueService.AcknowledgeAsync` sets `DateTime.UtcNow` client-side
- No `ILogger<T>` in any client service

---

## 1.2 Sibling Document Inventory

This file is the **single source of truth** for controller / client-service grading and remediation. The 10 sibling characterization and review documents in [`docs/controller-analysis/`](./) have been folded into this evaluation. Their unique recommendations are captured in §6 Recs #25–#35 below. The table records each sibling's disposition.

| Sibling Doc | Disposition | Notes |
|---|---|---|
| [authorities-controller-characterization.md](./authorities-controller-characterization.md) | ✅ Folded in | `ExecuteDeleteAsync` + ETag conditional GET → Rec #29. |
| [bookmarks-controller-characterization.md](./bookmarks-controller-characterization.md) | ✅ Folded in | `"test-user-id"` claim fallback → Rec #26. |
| [cases-controller-characterization.md](./cases-controller-characterization.md) | ✅ Folded in | `"test-user-id"` fallback → Rec #26; `IncludeAllNavigations()` on PATCH/POST/single-GET → Rec #30; `ResponseCache(Duration=60)` on mutable navigation collections → Rec #31. |
| [controller-endpoint-inventory.md](./controller-endpoint-inventory.md) | 📚 Reference only | Inventory remains useful as a route-table reference. Does not duplicate `Program.cs` `MapGet("/me")` — see Rec #14 (N2). |
| [documents-controller-characterization.md](./documents-controller-characterization.md) | ✅ Folded in | Five weaknesses already implemented (transactional upload, ETag via EDM, MimeMap, ProblemDetails, audit interceptor). Remaining: blob storage migration → Rec #32; download streaming → Rec #33. Resource-level authorization captured under existing Rec #8; rate limiting on upload/PDF under existing Rec #9. |
| [documents-controller-recommendations.md](./documents-controller-recommendations.md) | ✅ Folded in | Phase 1 (`SingleResult` + `ExecuteDeleteAsync`) and Phase 2 (`Delta<T>` PATCH + ETag) **already implemented** — sibling doc is partly stale. Phase 3 (migrate `ODataControllerBase` → `ControllerBase`) → Rec #35 (DEFER). LoggingService EventIds 312–315 are in scope of existing logging convention. |
| [members-controller-characterization.md](./members-controller-characterization.md) | ✅ Folded in | **🔴 PATCH RowVersion concurrency bug → Rec #25 (Critical)**; `Get(key)` materialisation → Rec #27; Delete two-roundtrip → Rec #28; ModelState leakage → Rec #34. PUT/PATCH duplication captured under existing §2.6 Remediation. Triple serialization surface (POST STJ / PUT STJ / PATCH OData) is a specific instance of existing Recs #5/#6. |
| [odata-controller-design-review.md](./odata-controller-design-review.md) | ✅ Folded in | `IncludeAllNavigations()` on PATCH/POST/single-GET (Concern #1) → Rec #30; `ResponseCache(Duration=60)` staleness on mutable collections (Concern #3) → Rec #31. Concerns #2 (`$select` on detail GET) and #4 (`$select` not pushed through `IncludeAllNavigations`) judged acceptable per the design review itself. |
| [user-controller-characterization.md](./user-controller-characterization.md) | ✅ Folded in | Commented `[Authorize]` + `"test-user-id"` fallback captured under existing §2.9 + Rec #26. `LookupUsers` N+1 already capped at `Take(50)` per §2.9. Sibling doc references `GET /api/User/me` only — `Program.cs` `MapGet("/me")` duplicate captured under Rec #14 (N2). |
| [workflow-state-history-controller-characterization.md](./workflow-state-history-controller-characterization.md) | 🗄 **Archive candidate — stale** | Source code already uses `Ok(SingleResult.Create(...))` on `Get`, restricts PATCH to `ExitDate` via `GetChangedPropertyNames().Contains("ExitDate")`, captures `originalRowVersion` before `delta.Patch`, maps `DbUpdateConcurrencyException` → 409, and applies `[ResponseCache(NoStore = true)]`. Only still-valid finding is N1 (client-supplied `EnteredDate` via `CreateWorkflowStateHistoryDto`) → Rec #22. Recommend archiving or rewriting this sibling doc. |

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
| 🔴 High | Audit Integrity | 🆕 `WorkflowStateHistoryController` accepts client-supplied `EntryDate` / `ExitDate` (N1) |
| 🔴 High | Authorization | 🆕 `CaseDialogueCommentsController.Patch` allows any user to edit any comment (N5) |
| 🔴 High | Performance | `WorkflowHistoryService.AddHistoryEntriesAsync` + `AuthorityService.SaveAuthoritiesAsync` use N+1 sequential POSTs instead of OData batch |
| 🔴 High | Reliability | Several client services swallow exceptions silently with no logging |
| 🔴 High | Serialization | Three distinct serialization pipelines (OData / MVC JSON / Minimal API JSON) with different property naming and enum handling — no unified configuration |
| 🟡 Medium | API Design | 🆕 Duplicate `/me` endpoints with divergent payloads (N2) |
| 🟡 Medium | API Design | Inconsistent error response format across controllers (mix of `Problem()` and raw status codes) |
| 🟡 Medium | Performance | `CaseService.SaveCaseAsync` navigation property detach/restore pattern is fragile |
| 🟡 Medium | Security | Token storage in `localStorage` is vulnerable to XSS |
| 🟡 Medium | Memory | 🆕 `UserService._cache` has no eviction policy (N3) |
| 🟡 Medium | Hygiene | 🆕 Stale `NotificationsController` reference — entity wired through API EDM, client EDM, and `CaseService.FullExpand` but no controller exists (N4) |
| ⚠️ Medium | Serialization | DI-registered `JsonSerializerOptions` on client is never consumed; `ODataServiceBase.JsonOptions` static field used instead |
| 🟢 Low | Reliability | 🆕 Bare `catch { }` in `BookmarkCountService` + `CurrentUserService`; no `SemaphoreSlim` around lazy init in `CurrentUserService` (N6) |
| 🟢 Low | Consistency | Dual OData client + HttpClient pattern in services adds cognitive overhead |
| 🟢 Low | Observability | Client services lack structured logging/telemetry |

---

## 2. API Controllers

### 2.1 ODataControllerBase — ✅ Completed

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

**Remediation Plan:**

1. Convert the `protected readonly` fields (`LoggingService`, `ContextFactory`, `TimeProvider`) to `protected` properties with `{ get; }` accessors so derived controllers see idiomatic C# members.
2. No behavior change is required — this is a hygiene refactor that can land in a single PR.

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

**Remediation Plan:**

1. Add `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` to the GET collection action to match `BookmarksController` / `DocumentsController`.
2. Move `X-Case-IsBookmarked` off the entity GET — expose it as a bound function `Cases({key})/Default.IsBookmarked` (or as an OData annotation on the response) so user-specific state isn't leaking through entity headers.
3. Add exponential backoff with jitter to the `CaseId` unique-constraint retry loop (e.g. `50ms * 2^attempt + rand(0,50)ms`) and cap at the existing 10 attempts to avoid thundering-herd collisions under concurrency.

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

**Remediation Plan:**

1. In the `Form348` action, validate the parent case exists **and** the authenticated user is authorized against it (resource-based policy) before invoking PDF generation.
2. Persist failed blob-deletion attempts to an `OrphanedBlob` table (or queue) and process them on a periodic background job; emit a metric for orphans created/cleared.
3. Add structured `LoggerMessage` events for orphan creation/cleanup so ops can alert on growth.

---

### 2.4 BookmarksController — ✅ Completed

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

**Remediation Plan:**

1. No code changes required. Lock the current contract in integration tests: idempotent POST, 404 on cross-user DELETE, `NoStore` cache on GET.

---

### 2.5 AuthoritiesController

**File:** `ECTSystem.Api/Controllers/AuthoritiesController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData `Delta<T>.Patch()` for partial updates. |
| ResponseCache | ✅ **Correct** | `NoStore = true` on reads. |
| CancellationToken | ✅ **Correct** | Propagated. |

**Findings:**

- ⚠️ **No ownership validation** — Unlike `BookmarksController`, authorities can be created/updated/deleted without verifying the caller is authorized. This is a potential authorization gap. **Per current policy, gate all write operations on the `Admin` role for now**; resource/ownership-based policies can be revisited later when additional roles are introduced.

**Remediation Plan:**

1. Apply `[Authorize(Roles = "Admin")]` to POST, PATCH, and DELETE actions in `AuthoritiesController`; return 403 on failure. (A resource-based `CaseAccessRequirement` policy is deferred until non-Admin roles exist.)
2. Document the Admin-only restriction in the controller XML doc and ensure the client UI hides write affordances for non-Admin users.
3. Cover the role check with controller-level integration tests that exercise both allow (Admin) and deny (non-Admin) paths.

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
- 🔴 **PATCH RowVersion concurrency check is silently bypassed** — `delta.Patch(existing)` overwrites `existing.RowVersion` with the client value before the next line copies it into `OriginalValue`, so the concurrency token is compared against itself and always passes. Folded in from [members-controller-characterization.md](./members-controller-characterization.md) §3 → Rec #25.
- ⚠️ **`Get(key)` materialises the entity** — uses `FirstOrDefaultAsync` instead of `SingleResult.Create(...)`, defeating `$select`/`$expand` and routing the response through System.Text.Json instead of the OData formatter (causing the same entity to serialise enums differently from a collection GET). Folded in → Rec #27.
- ⚠️ **DELETE has no concurrency guard and uses two round trips** — `FindAsync` + `Remove` + `SaveChanges` instead of a single `ExecuteDeleteAsync`. A stale delete succeeds silently. Folded in → Rec #28.
- 🟢 **POST `BadRequest(ModelState)` may leak inner exception messages** to the client. Folded in → Rec #34.

**Remediation Plan:**

1. **Critical first:** Capture `var originalRowVersion = existing.RowVersion;` before `delta.Patch(existing)` and use that captured value when setting `Property(e => e.RowVersion).OriginalValue` (Rec #25).
2. Choose PATCH as the partial-update verb and remove PUT (or vice versa) to align with Microsoft REST guidelines; update `MemberService` callers accordingly.
3. Add `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` on every GET endpoint that returns Member PII (single, collection, navigation).
4. Convert `Get(key)` to `Ok(SingleResult.Create(context.Members.AsNoTracking().Where(m => m.Id == key)))` so OData composes `$select`/`$expand` (Rec #27).
5. Replace `Delete` body with `var deleted = await context.Members.Where(m => m.Id == key).ExecuteDeleteAsync(ct); return deleted == 0 ? NotFound() : NoContent();`, optionally guarded by an `If-Match` RowVersion check (Rec #28).
6. Sanitise the POST error response: log full `ModelState` (with exception messages) via `LoggingService`, return a `ValidationProblem` with only the user-facing error strings (Rec #34).
7. Add a regression test that asserts `Cache-Control: no-store` is present on member responses, and a PATCH concurrency test that fails when stale RowVersion is sent.

---

### 2.7 WorkflowStateHistoryController

**File:** `ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Restricted PATCH | ✅ **Good** | Only allows updating `ExitDate` — prevents tampering with history records. |

**Findings:**

- ✅ Good pattern — restricting which properties can be patched on a history/audit entity.
- 🔴 **Client-supplied audit dates (N1)** — POST and PATCH currently accept `EntryDate` / `ExitDate` from the request body, allowing clients to backdate or forward-date audit records.

**Remediation Plan:**

1. On POST, overwrite `EntryDate` server-side from `TimeProvider.GetUtcNow()` regardless of the supplied value.
2. On PATCH, ignore any `Delta<T>` property other than `ExitDate`, and overwrite `ExitDate` server-side from `TimeProvider`.
3. Add a unit test asserting that a client-supplied `EntryDate`/`ExitDate` is ignored.
4. Update `WorkflowHistoryService.AddHistoryEntriesAsync` and `UpdateHistoryEndDateAsync` to stop sending these timestamps from the client (see §3.8 plan).

---

### 2.8 CaseDialogueCommentsController

**File:** `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Parent Validation | ✅ **Correct** | Validates the parent case exists before creating a comment. |
| Author-Scoped Delete | ⚠️ **Revisit** | Currently allows comment author to delete; per current policy, restrict to `Admin` only. |
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData Delta. |

**Findings:**

- ⚠️ **DELETE author-scoping should be replaced with `Admin`-only authorization** per current policy (only the `Admin` role is in use).
- ⚠️ **PATCH not authorized** — Any authenticated user can PATCH any comment (e.g., to acknowledge it). If acknowledge is the only valid patch operation, consider a dedicated bound action instead (N5).

**Remediation Plan:**

1. **Short term:** Apply `[Authorize(Roles = "Admin")]` to both PATCH and DELETE — only `Admin` users may modify or remove comments for now. Author-scoped checks can be reintroduced when additional roles exist.
2. **Preferred long term:** Replace the freeform PATCH with a bound action `Comments({key})/Default.Acknowledge` that requires no body, is idempotent, and stamps the acknowledgement timestamp server-side via `TimeProvider`.
3. Update `CaseDialogueService.AcknowledgeAsync` to call the bound action and stop sending `DateTime.UtcNow` from the client (see §3.9 plan).

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
- ℹ️ **Duplicate `/me` endpoint (N2)** — `Program.cs` maps `app.MapGet("/me", ...)` and `UserController` maps `api/User/me`. The controller version returns `CurrentUserDto` (PascalCase, string enums); the Program.cs version returns `{ Name }` (camelCase, integer enums). Two pipelines, two shapes.

**Remediation Plan:**

1. Treat `GET api/User/me` as canonical. Remove the `app.MapGet("/me", ...)` block from `Program.cs`.
2. Audit callers (`CurrentUserService`, any tests, any docs) and repoint them at `api/User/me`.
3. Add a regression test that asserts only one `/me`-style route exists in the route table.

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

**Remediation Plan:**

Apply the four fixes in the order listed; each step builds on the prior one. After step 1 the solution will not compile until derived service constructors are updated, so plan to complete steps 1–2 in a single pass.

#### Fix 1 — Resolve dual abstraction (OData client vs. raw HttpClient)

Establish and document a convention on the base class:

- **`Context` (OData client)** — all reads/queries (`$filter`, `$top`, `$skip`, `$expand`, `$count`, navigation collections).
- **`HttpClient`** — only what the typed client cannot model: bound actions, `$batch`, multipart uploads, `PATCH` against arbitrary JSON.

Add typed helpers to the base so derived services stop hand-rolling raw HTTP for common write/action cases:

```csharp
protected async Task<TResponse?> PostActionAsync<TRequest, TResponse>(
    string relativeUrl, TRequest body, CancellationToken ct = default)
{
    using var response = await HttpClient.PostAsJsonAsync(relativeUrl, body, JsonOptions, ct);
    response.EnsureSuccessStatusCode();
    return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, ct);
}

protected async Task PatchEntityAsync<T>(string relativeUrl, T patch, CancellationToken ct = default)
{
    using var msg = new HttpRequestMessage(HttpMethod.Patch, relativeUrl)
    {
        Content = JsonContent.Create(patch, options: JsonOptions)
    };
    using var response = await HttpClient.SendAsync(msg, ct);
    response.EnsureSuccessStatusCode();
}
```

Migrate per-service raw HTTP boilerplate onto these helpers. Add an XML doc comment on `ODataServiceBase` stating the convention so future contributors don't reintroduce the split.

#### Fix 2 — Add error handling to base helpers

Wrap the OData client calls and translate framework exceptions into a single typed exception. Inject `ILogger` so the boundary logs once. Let `OperationCanceledException` propagate untouched.

```csharp
public sealed class ODataClientException : Exception
{
    public int? StatusCode { get; }
    public ODataClientException(string message, int? statusCode, Exception inner)
        : base(message, inner) => StatusCode = statusCode;
}

protected async Task<(IReadOnlyList<T> Items, int Count)> ExecutePagedQueryAsync<T>(
    DataServiceQuery<T> query, CancellationToken ct = default)
{
    try
    {
        var response = (QueryOperationResponse<T>)await query.IncludeCount().ExecuteAsync(ct);
        return (response.ToList(), (int)response.Count);
    }
    catch (DataServiceQueryException ex)
    {
        Logger.LogWarning(ex, "OData query failed: {Uri}", query.RequestUri);
        throw new ODataClientException("OData query failed.", (int?)ex.Response?.StatusCode, ex);
    }
    catch (DataServiceClientException ex)
    {
        Logger.LogWarning(ex, "OData client error.");
        throw new ODataClientException("OData client error.", ex.StatusCode, ex);
    }
    catch (HttpRequestException ex)
    {
        Logger.LogWarning(ex, "HTTP error executing OData query: {Uri}", query.RequestUri);
        throw new ODataClientException("Network error.", (int?)ex.StatusCode, ex);
    }
}
```

Apply the same pattern to `ExecuteQueryAsync`. Convert the helpers from `static` to instance methods so they can use the injected `Logger`.

#### Fix 3 — Eliminate `ODataCountResponse<T>` / `ODataResponse<T>` duplication

These envelope classes exist because some services use `HttpClient.GetFromJsonAsync<ODataCountResponse<T>>(...)` to read paged collections instead of using the typed OData client. The OData client already does this work via `IncludeCount()` + `QueryOperationResponse<T>.Count`:

```csharp
// Replace raw HTTP + ODataCountResponse<T> with:
var (items, count) = await ExecutePagedQueryAsync(
    Context.CreateQuery<Foo>("Foos")
           .AddQueryOption("$filter", filter)
           .AddQueryOption("$top", top.ToString())
           .AddQueryOption("$skip", skip.ToString()),
    ct);
```

Audit each derived service for `ODataCountResponse<T>` / `ODataResponse<T>` references and convert them to `DataServiceQuery<T>` calls. Once nothing references the envelope classes, delete them. Keep them only for endpoints the OData client genuinely cannot model (e.g., custom server-side projection responses).

#### Fix 4 — Inject DI-registered `JsonSerializerOptions`; remove the static field

The DI singleton in `AddJsonSerializerOptions()` ([ServiceCollectionExtensions.cs](../ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs#L36-L43)) already configures `JsonStringEnumConverter` + `ReferenceHandler.IgnoreCycles`. Inject it instead of duplicating config and silently omitting `IgnoreCycles`.

```csharp
public abstract class ODataServiceBase
{
    protected EctODataContext Context { get; }
    protected HttpClient HttpClient { get; }
    protected JsonSerializerOptions JsonOptions { get; }
    protected ILogger Logger { get; }

    protected ODataServiceBase(
        EctODataContext context,
        HttpClient httpClient,
        JsonSerializerOptions jsonOptions,
        ILogger logger)
    {
        Context = context;
        HttpClient = httpClient;
        JsonOptions = jsonOptions;
        Logger = logger;
    }
    // remove: protected static readonly JsonSerializerOptions JsonOptions = new() { ... };
}
```

Update every derived service constructor to forward the new parameters:

```csharp
public sealed class CaseService : ODataServiceBase, ICaseService
{
    public CaseService(EctODataContext ctx, HttpClient http,
                      JsonSerializerOptions json, ILogger<CaseService> logger)
        : base(ctx, http, json, logger) { }
}
```

#### Suggested order of work

1. Change base ctor to inject `JsonSerializerOptions` + `ILogger`; remove the static field. Update all derived services' constructors. Build to surface every call site.
2. Convert query helpers to instance methods and add the try/catch wrappers + `ODataClientException`.
3. Sweep derived services replacing raw `HttpClient.GetFromJsonAsync<ODataCountResponse<T>>(...)` with `ExecutePagedQueryAsync(...)`. Delete the envelope classes when unused.
4. Add `PostActionAsync` / `PatchEntityAsync` helpers; migrate per-service raw HTTP write code onto them. Add the convention XML doc on the base class.

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

**Remediation Plan:**

1. Add a startup self-check (run once during `Program.cs` host build) that walks the registered client EDM model and asserts every entity type has both an entity-set name and a CLR type mapping; fail-fast on mismatch.
2. As a follow-up, replace the hand-maintained dictionaries by reflecting over `IEdmModel` so the maps cannot drift.

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

**Remediation Plan:**

1. Replace the navigation detach/restore in `SaveCaseAsync` with a `JsonTypeInfoResolver` modifier that strips navigation properties at serialization time (the approach already noted in `Program.cs` comments). Remove the `finally`-block restoration.
2. Standardize the checkout API: have `CheckOutCaseAsync` / `CheckInCaseAsync` return a `CheckoutResult` discriminated value (`Success` / `Conflict` / `NotFound`) and throw on transport errors, instead of returning `bool` for all outcomes.
3. Add an XML-doc comment on `GetCasesByCurrentStateAsync` explaining why it bypasses the OData client (bound collection action limitation), and reference the same pattern used in `BookmarkService.ByCurrentState`.
4. Either implement a `NotificationsController` or remove `Notification` from `FullExpand` (see N4 / Rec #24).

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

**Remediation Plan:**

1. Add a server-side bound action `POST /odata/Cases({key})/Default.SaveAuthorities` that accepts the full `IEnumerable<AuthorityDto>` and performs add/update/delete inside a single EF transaction.
2. Replace `SaveAuthoritiesAsync` with a single call to that action; delete the per-item PATCH/POST/DELETE orchestration and the local detach loop.
3. Cover happy path + partial-failure rollback in API integration tests.

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

**Remediation Plan:**

1. Extend the existing `ByCurrentState` bound action (or add a sibling) to accept an optional `bookmarkedOnly: bool` parameter, eliminating the two-trip pattern in `GetBookmarkedCasesByCurrentStateAsync`.
2. Replace `IsBookmarkedAsync` with a header read from the existing `Cases({key})` GET (`X-Case-IsBookmarked`) so a separate bookmark query is not needed when the caller already loaded the case.
3. For standalone callers, fall back to `$top=0&$count=true` instead of fetching rows.

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

**Remediation Plan:**

1. Confirm the download endpoint accepts the bearer token via `Authorization` header (current `HttpClient` use), and have UI download flows go through `HttpClient` + blob URL rather than a raw `<a href>` to the API.
2. If direct anchor downloads are required, expose a short-lived signed URL action (`Documents({key})/Default.GetSignedUrl`) that returns a one-time, scoped URL — never accept tokens via query string.

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

**Remediation Plan:**

1. Add a server-side action `POST /odata/Members/Default.Search` accepting `{ term, top }`, performing the multi-field `contains` filter (and rank-to-paygrade / component matching) on the server using indexed columns or full-text search.
2. Reduce `MemberService.SearchMembersAsync` to a single call against the new action, dropping the client-side filter builder and regex enum matching.
3. Keep the existing `'` → `''` quoting (✅ already in place) on the new action's input parameter as defense in depth.

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

**Remediation Plan:**

1. Replace the per-entry POST loop in `AddHistoryEntriesAsync` with an OData `$batch` request — the API already wires `DefaultODataBatchHandler` and `app.UseODataBatching()`, so the client just needs to use `DataServiceContext.SaveChanges(SaveChangesOptions.BatchWithSingleChangeset)` (or equivalent batched path).
2. Stop sending `EntryDate` / `ExitDate` from the client; rely on the server (`TimeProvider`) to stamp them once §2.7's plan lands (N1).
3. Inject `ILogger<WorkflowHistoryService>` and emit a structured event for each batch outcome.

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

**Remediation Plan:**

1. Drop `DateTime.UtcNow` from `AcknowledgeAsync`; switch to a server-side bound action (`Comments({key})/Default.Acknowledge`) that stamps the timestamp via `TimeProvider` and returns the updated entity (see §2.8 plan).
2. Extract the manual `HttpRequestMessage` PATCH pattern into an `ODataServiceBase.PatchAsync<T>(uri, payload, ct)` helper so service code stays declarative.

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

**Remediation Plan:**

1. Add a `TokenRefreshHandler : DelegatingHandler` that, on a 401 response, calls the refresh-token endpoint, updates `localStorage`, and retries the original request **once**; bail out (and force logout) if the refresh fails.
2. Register the handler on both the `Api` and `OData` named clients in `ServiceCollectionExtensions`.
3. Document the `localStorage` XSS trade-off in a comment on `AuthService`, referencing the existing CSP and the lack of viable `httpOnly` cookie path in standalone Blazor WASM.

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

**Remediation Plan:**

1. Replace `Dictionary<string, string> _cache` with `IMemoryCache` configured with a sliding expiration (e.g., 10 minutes) and a size limit (e.g., 500 entries) — this prevents unbounded growth and stale display names (N3).
2. Add a `Clear()` method invoked by `AuthService.LogoutAsync` so the cache is reset between users.
3. Cover cache eviction with a unit test that verifies entries expire after the configured window.

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

**Remediation Plan:**

1. Wrap the lazy fetch with a `SemaphoreSlim(1, 1)` (or store a `Lazy<Task<string?>>` rebuilt on `Clear()`) so concurrent components share a single `/me` request (N6).
2. Replace the bare `catch { }` with `catch (Exception ex)` that logs via injected `ILogger<CurrentUserService>` before re-throwing or returning `null`.
3. Repoint at the canonical `api/User/me` once the duplicate `/me` is removed (see §2.9 plan).

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

**Remediation Plan:**

1. Make `Increment()` / `Decrement()` private and only call them from `BookmarkService` **after** the server operation has succeeded; on failure, leave the count unchanged and let the next `RefreshAsync()` self-correct.
2. Replace the bare `catch` with `catch (HttpRequestException ex)` (and `OperationCanceledException` where relevant); log via injected `ILogger<BookmarkCountService>` instead of swallowing.
3. Add an integration test that verifies the badge count stays consistent across an `AddBookmark` → server-error → next-refresh cycle.

---

## 4. Cross-Cutting Concerns

### 4.1 Security

| Aspect | Status | Details |
|--------|--------|---------|
| Authentication | ✅ | All controllers use `[Authorize]`. Identity API endpoints properly mapped. |
| Authorization Policies | ⚠️ | `Admin`, `CaseManager`, `CanManageDocuments` policies are defined, but **per current policy only the `Admin` role is in active use**. New role-gated endpoints should use `[Authorize(Roles = "Admin")]` until additional roles are reintroduced. |
| Security Headers | ✅ | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `CSP` all set. |
| CORS | ✅ | Origin allowlist, exposed headers, `AllowCredentials()`. |
| File Upload | ✅ | Extension allowlist + magic bytes + size limit. |
| PII Scrubbing | ✅ | SSN patterns scrubbed from request/response logs. |
| Password Policy | 🔴 | `RequireDigit=false`, `RequireUppercase=false`, `RequireNonAlphanumeric=false`, `RequiredLength=6`. For a military LOD system handling PII, this is too weak. Increase to minimum 12 characters with complexity requirements per DoD guidance. |
| Rate Limiting | ⚠️ | Defined but commented out (`AddApiRateLimiting`, `UseRateLimiter`). Should be enabled for production. |
| HTTPS Redirection | ✅ | `UseHttpsRedirection()` in pipeline. |

**Remediation Plan:**

1. Strengthen Identity password options to DoD-aligned defaults: `RequireDigit = true`, `RequireUppercase = true`, `RequireNonAlphanumeric = true`, `RequiredLength = 12`, `RequiredUniqueChars = 4`.
2. Re-enable `AddApiRateLimiting()` registration and `app.UseRateLimiter()` in `Program.cs`; tune the policy for the Identity endpoints (`/login`, `/refresh`) and the search-heavy OData routes.
3. Add an integration test asserting that `/login` is rejected with HTTP 429 after exceeding the configured limit.

### 4.2 Error Handling & Problem Details ✅ Completed

| Aspect | Status | Details |
|--------|--------|---------|
| Global Exception Handler | ✅ | Production: `UseExceptionHandler` with ProblemDetails-style JSON. Dev: `UseDeveloperExceptionPage`. |
| OperationCancelled | ✅ | Dedicated middleware returns 499 instead of 500 for client disconnects. |
| UnauthorizedAccess | ✅ | Dedicated middleware returns 401 with RFC 7235 format. |
| Controller Error Format | ✅ | Standardized on `Problem(...)` / `ValidationProblem(...)`. Replaced stray `BadRequest(string)` returns in `BookmarksController.AddBookmark` and `DeleteBookmark` with `Problem(title, detail, statusCode)` calls. The `StatusCode(304)` in `CasesController.GetCase` is intentional caching response, not an error path. |
| Client Error Handling | ✅ | Added `EctApiException` (carries `HttpStatusCode` + parsed `ApiProblemDetails`) and `ODataServiceBase.EnsureSuccessOrThrowAsync` / `TryReadProblemDetailsAsync` helpers. All OData services (`AuthorityService`, `BookmarkService`, `CaseDialogueService`, `CaseService`, `DocumentService`, `WorkflowHistoryService`) now route failures through the helper. `AuthService` retains its existing discriminated `AuthResult` contract. `CheckOutCaseAsync` / `CheckInCaseAsync` still return `bool` per their interface but now log structured warnings (with parsed problem details) before returning false. |

**Remediation Plan:**

1. ✅ Pick `Problem(...)` (RFC 7807) as the canonical controller error helper and replace stray `StatusCode(...)` / `NotFound(...)` returns with `Problem(...)` or `ValidationProblem(...)` overloads carrying a stable `type`/`title`. — Audit confirmed controllers already used `Problem(...)` / `ValidationProblem(...)` almost everywhere; the two remaining `BadRequest(string)` returns in `BookmarksController` were converted to `Problem(...)`. The lone `StatusCode(StatusCodes.Status304NotModified)` in `CasesController` is a caching success path and intentionally preserved.
2. ✅ Define a client-side error contract: services either return a result-style discriminated value or throw a dedicated exception type — never silently return `null`/`false` for transport failures. — Introduced `EctApiException` (`ECTSystem.Web.Services`) wrapping `ApiProblemDetails`. `AuthService` continues to use its `AuthResult { Succeeded, Error }` discriminated value. `CheckOutCaseAsync` / `CheckInCaseAsync` still return `bool` (interface contract unchanged) but now emit structured `LogWarning` entries containing the parsed problem details so failures are no longer silent.
3. ✅ Add an `IProblemDetailsReader` helper on `ODataServiceBase` so client services can surface server-issued `ProblemDetails` to the UI consistently. — Implemented as two `protected` methods on `ODataServiceBase`: `TryReadProblemDetailsAsync(HttpResponseMessage, CancellationToken)` (safe parse, returns `null` on failure) and `EnsureSuccessOrThrowAsync(HttpResponseMessage, string operation, CancellationToken)` (logs + throws `EctApiException` on non-success). All `response.EnsureSuccessStatusCode()` and ad-hoc `throw new HttpRequestException(...)` call sites in OData services were migrated to the helper.

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

**Remediation Plan:**

1. Adopt OData `$batch` on the client — enable `SaveChangesOptions.BatchWithSingleChangeset` (or use `BatchAsync`) for the multi-write services (`AuthorityService`, `WorkflowHistoryService`).
2. Audit OData reads and add `$select` (and `$expand` only as needed) to the heavy entity reads (e.g., `LineOfDutyCase` lists, `Member` searches) to shrink payloads.
3. Add a benchmark or profiler trace verifying batch + `$select` reductions before/after.

### 4.4 Structured Logging ✅ Completed

| Aspect | Status | Details |
|--------|--------|---------|
| Server Logging | ✅ | `LoggerMessage` source generators via `ILoggingService` — high-performance structured logging. |
| Request Logging | ✅ | `RequestLoggingMiddleware` with timing, method, path, status code. Body logging in dev only. |
| Client Logging | ✅ | `ILogger<T>` injected into every client service via `ODataServiceBase` and standalone services. Bare `catch { }` blocks replaced with `catch (Exception ex)` + structured `LogWarning`. |

**Remediation Plan:**

1. ✅ `ILogger<T>` injected into every client service (`CaseService`, `BookmarkService`, `AuthorityService`, `MemberService`, `WorkflowHistoryService`, `CaseDialogueService`, `AuthService`, `UserService`, `CurrentUserService`, `BookmarkCountService`, `DocumentService`).
2. ✅ `ODataServiceBase` exposes a `protected readonly ILogger Logger` field via constructor injection so all OData-derived services share a single logger entry point for outbound-call diagnostics.
3. ✅ Bare `catch { }` blocks in `BookmarkCountService.RefreshAsync` and `CurrentUserService.GetUserIdAsync` replaced with `catch (Exception ex)` + `_logger.LogWarning(ex, ...)` using named placeholders. `CurrentUserService` also gained a `SemaphoreSlim` to serialize the lazy `_userId` initialization.

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
| **OData Serializer** | 7 OData controllers (`Cases`, `Documents`, `Bookmarks`, `Authorities`, `Members`, `WorkflowStateHistory`, `CaseDialogueComments`). Note: the `Notification` `EntitySet` is registered in the EDM but **no `NotificationsController` exists** — see issue N4. | Driven by EDM model via `BuildEdmModel()` in `ServiceCollectionExtensions.cs` | PascalCase (follows CLR property names in EDM) | String names by default (EDM enum type definitions) |
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
| 22 🆕 | **Workflow audit timestamps client-supplied** (N1) | `WorkflowStateHistoryController` POST/PATCH | Server must set `EntryDate` / `ExitDate` from `TimeProvider.GetUtcNow()` and reject (or ignore) client values. Audit trail integrity requires monotonic, server-issued timestamps. |
| 23 🆕 | **`CaseDialogueCommentsController.Patch` and `Delete` should require `Admin`** (N5) | Same controller, PATCH + DELETE actions | Per current policy, restrict both `PATCH` and `DELETE` to `[Authorize(Roles = "Admin")]`. The existing author-scoped DELETE check should be replaced (not augmented) with the Admin-only check; author scoping can be reintroduced when non-Admin roles are added. |

### 🟡 Medium Priority

| # | Issue | Location | Recommendation |
|---|-------|----------|----------------|
| 7 | **Navigation property detach/restore** | `CaseService.SaveCaseAsync` | Replace manual null/restore with `JsonTypeInfoResolver` modifier to exclude navigation properties at serialization time. |
| 8 | **Missing role-based authorization** | `AuthoritiesController`, `CaseDialogueCommentsController` PATCH | Per current policy, gate writes on `[Authorize(Roles = "Admin")]`. Resource/ownership-based policies are deferred until additional roles are introduced. |
| 9 | **Enable rate limiting** | `Program.cs` | Uncomment `AddApiRateLimiting()` and `UseRateLimiter()` for production. |
| 10 | **Inconsistent error responses** | Multiple controllers | Standardize on `Problem()` for all error responses (RFC 7807). |
| 11 | **Missing `ResponseCache` attribute** | `CasesController.Get` (collection), `MembersController` | Add `[ResponseCache(NoStore = true)]` to endpoints returning PII. |
| 12 | **Client error handling strategy** | Multiple client services | Establish a convention: return `Result<T>` or throw typed exceptions. Avoid swallowing errors silently or inconsistently returning `null`/`false`. |
| 13 | **Token refresh mechanism** | `AuthService` | Implement a `DelegatingHandler` that intercepts 401 responses and automatically retries with the stored refresh token before failing. |
| 14 | **Duplicate `/me` endpoint** 🆕 confirmed (N2) | `Program.cs` + `UserController` | Remove the `app.MapGet("/me", ...)` in `Program.cs` — the `UserController.GetCurrentUser` is the authoritative version. The two endpoints emit different shapes (`{ Name }` vs. `CurrentUserDto { UserId, Name }`) and use different JSON pipelines. |
| 24 🆕 | **Orphaned `Notification` plumbing** (N4) | API EDM, client EDM, `EctODataContext.Notifications`, `CaseService.FullExpand` | Either (a) add a `NotificationsController` with proper authorization to expose the entity set, or (b) remove the entity set, the client `Notifications` query property, and `Notification` from `CaseService.FullExpand` to eliminate dead OData metadata. |

### 🟢 Low Priority

| # | Issue | Location | Recommendation |
|---|-------|----------|----------------|
| 15 | **Move search logic server-side** | `MemberService.SearchMembersAsync` | Create a `POST /odata/Members/Search` action that accepts a search term. |
| 16 | **Consolidate bookmark state query** | `BookmarkService.GetBookmarkedCasesByCurrentStateAsync` | Create a server-side action that combines bookmark + workflow state filtering in one query. |
| 17 | **Thread-safe lazy init** 🆕 confirmed (N6) | `CurrentUserService.GetUserIdAsync` | Use `SemaphoreSlim` or `Lazy<Task<T>>` to prevent duplicate HTTP requests on concurrent access. Replace bare `catch { }` with logged exception handling. |
| 18 | **Cache TTL for UserService** 🆕 confirmed (N3) | `UserService._cache` | Add time-based expiry or size limit (e.g., `MemoryCache` with `SlidingExpiration`) to prevent unbounded growth in long sessions. |
| 19 | **Protected fields → properties** | `ODataControllerBase` | Change `protected readonly` fields to `protected` properties for idiomatic C#. |
| 20 | **OData client vs HttpClient convention** | `ODataServiceBase` and derived services | Document when to use each. Consider standardizing: OData client for reads, HttpClient for writes/actions. |
| 21 | **Add serialization round-trip integration tests** | `ECTSystem.Tests` | Verify that entities serialized by the API (OData, MVC, and minimal API) can be deserialized by the client with all three `JsonSerializerOptions` configurations. |
| 35 🆕 | **Defer `ODataControllerBase` → `ControllerBase` migration on `DocumentsController`** | `DocumentsController`, `ODataControllerBase` | Per [documents-controller-recommendations.md](./documents-controller-recommendations.md) Phase 3, this rewrite is high-risk (loses OData routing, `Delta<T>` model binding, `[EnableQuery]` integration) and offers limited benefit now that Phase 1 + Phase 2 are implemented. **Decision: Defer.** Revisit only if/when other controllers migrate off OData. |

### 🆕 Consolidated From Sibling Documents

These recommendations were harvested from the per-controller characterization and review docs and were not yet represented in §6.

#### 🔴 High Priority (added)

| # | Issue | Location | Recommendation | Source |
|---|-------|----------|----------------|--------|
| 25 🆕 | **`MembersController.Patch` RowVersion concurrency check silently bypassed** | `MembersController.Patch` | `delta.Patch(existing)` overwrites `existing.RowVersion` with the client-supplied value **before** the next line copies it into `Entry(...).Property(e => e.RowVersion).OriginalValue` — the concurrency check ends up comparing the client value against itself and always succeeds. **Capture `var originalRowVersion = existing.RowVersion;` before `delta.Patch()` and pass that captured value to `OriginalValue`.** | [members-controller-characterization.md](./members-controller-characterization.md) §Weaknesses #3 |
| 26 🆕 | **`"test-user-id"` fallback when claim is missing in `CasesController` and `BookmarksController`** | `CasesController`, `BookmarksController` user-id resolution helpers | When `ClaimTypes.NameIdentifier` is absent the controllers currently fall back to the literal `"test-user-id"`. In production this masks a broken auth pipeline and silently scopes data to a non-existent shared identity. **Throw `UnauthorizedAccessException` (or return `Unauthorized()`) when the claim is missing**; the fallback should exist only in test fixtures, not in controller code. | [bookmarks-controller-characterization.md](./bookmarks-controller-characterization.md), [cases-controller-characterization.md](./cases-controller-characterization.md) |

#### 🟡 Medium Priority (added)

| # | Issue | Location | Recommendation | Source |
|---|-------|----------|----------------|--------|
| 27 🆕 | **`MembersController.Get(key)` materialises the entity** | `MembersController.Get(int key)` | Currently uses `FirstOrDefaultAsync`, which executes the query before OData can apply `$select`/`$expand` and serialises through System.Text.Json (integer enums) instead of the OData formatter (string enums) — the **same entity serialises differently** when fetched by key vs. via the collection. Convert to `return Ok(SingleResult.Create(context.Members.AsNoTracking().Where(m => m.Id == key)));`. | [members-controller-characterization.md](./members-controller-characterization.md) §Weaknesses #1 |
| 28 🆕 | **`MembersController.Delete` is two round-trips with no concurrency guard** | `MembersController.Delete` | Replace the `FindAsync` + `Remove` + `SaveChanges` pair with `var deleted = await context.Members.Where(m => m.Id == key).ExecuteDeleteAsync(ct); return deleted == 0 ? NotFound() : NoContent();`. Add an `If-Match` RowVersion check if optimistic concurrency on delete is required (mirror `CasesController`). | [members-controller-characterization.md](./members-controller-characterization.md) §Weaknesses #5 |
| 29 🆕 | **`AuthoritiesController` should use `ExecuteDeleteAsync` and add ETag-based conditional GET** | `AuthoritiesController` Delete + collection GET | DELETE currently uses `FindAsync` + `Remove` + `SaveChanges`; switch to `ExecuteDeleteAsync` for a single-statement delete. Because authorities are stable lookup data, the existing `[ResponseCache(Duration = 60)]` on the collection GET is appropriate — **augment it with `If-None-Match`/`ETag` conditional GET** so clients revalidate cheaply rather than always pulling the body. | [authorities-controller-characterization.md](./authorities-controller-characterization.md) |
| 30 🆕 | **`CasesController` calls `IncludeAllNavigations()` on PATCH / POST / single-GET responses** | `CasesController.Patch`, `Post`, `Get(key)` (10 `.Include()` graph) | Loading the full 10-navigation graph for a write response (or a single-entity GET when the client did not ask for `$expand`) wastes ~11 SQL round trips per call. **Return only the mutated entity** (no `IncludeAllNavigations()`) on PATCH/POST, and let `[EnableQuery]` + client `$expand` drive single-entity GETs. Keep `IncludeAllNavigations()` on `Delete` only because EF needs the cascade graph loaded. | [cases-controller-characterization.md](./cases-controller-characterization.md), [odata-controller-design-review.md](./odata-controller-design-review.md) Concern #1 |
| 31 🆕 | **`CasesController.GetDocuments` / `GetNotifications` / `GetWorkflowStateHistories` use `[ResponseCache(Duration = 60)]`** | Three navigation endpoints on `CasesController` | These collections mutate during active workflow processing (uploads, state transitions, comments). A 60-second client cache means newly uploaded documents and freshly emitted history entries are invisible for up to a minute — a UX and audit-integrity hazard. **Switch to `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`.** Reserve `Duration = 60` only for stable lookup data such as `AuthoritiesController` (Rec #29). | [cases-controller-characterization.md](./cases-controller-characterization.md), [odata-controller-design-review.md](./odata-controller-design-review.md) Concern #3 |

#### 🟢 Low Priority (added)

| # | Issue | Location | Recommendation | Source |
|---|-------|----------|----------------|--------|
| 32 🆕 | **`DocumentsController` stores binary content as `varbinary(max)`** | `LineOfDutyDocument.Content`, `DocumentsController` Upload/Get | Causes table & backup bloat, full-row locks during reads/writes, LOH allocations on upload, and no CDN tier for downloads. Introduce an `IBlobStorageService` abstraction (Azure Blob Storage), replace `Content` with a `BlobUri` column, stream `IFormFile.OpenReadStream()` directly to `BlobClient.UploadAsync`, and migrate existing rows in a backfill job. | [documents-controller-characterization.md](./documents-controller-characterization.md) §Weaknesses #1, #2 |
| 33 🆕 | **`DocumentsController.GetValue` buffers full blob into memory** | `DocumentsController.GetValue` | Loads `d.Content` into a byte array via EF then hands it to `File()`. With blob storage (Rec #32), redirect to a short-TTL SAS URL or stream from `BlobClient.OpenReadAsync()`. Without blob storage, use ADO.NET `CommandBehavior.SequentialAccess` + `reader.GetStream(0)` to stream from the column. | [documents-controller-characterization.md](./documents-controller-characterization.md) §Weaknesses #8 |
| 34 🆕 | **`MembersController.Post` returns `BadRequest(ModelState)` containing inner exception messages** | `MembersController.Post` | The same detailed error string used for logging is also returned to the client, potentially leaking internal exception text in development. Log the verbose form via `LoggingService.MemberInvalidModelState`, but return `ValidationProblem` with only the sanitised user-facing error messages. | [members-controller-characterization.md](./members-controller-characterization.md) §Weaknesses #6 |

---

## 7. Archive Candidates

- **[workflow-state-history-controller-characterization.md](./workflow-state-history-controller-characterization.md)** — Provably stale: the source code already implements `SingleResult.Create`, restricts PATCH to `ExitDate` via `GetChangedPropertyNames()`, captures `originalRowVersion` before `delta.Patch`, maps `DbUpdateConcurrencyException` → 409, and uses `[ResponseCache(NoStore = true)]`. The only remaining valid finding (client-supplied `EnteredDate` via `CreateWorkflowStateHistoryDto`) is captured here as N1 + Rec #22. **Recommend archiving** (move to `docs/controller-analysis/archive/`) or rewriting against current source.
- **[documents-controller-recommendations.md](./documents-controller-recommendations.md)** — Partly stale: Phase 1 (`SingleResult` + `ExecuteDeleteAsync`) and Phase 2 (`Delta<T>` PATCH with RowVersion + 409) are **already implemented** in `DocumentsController`. Phase 3 (`ODataController` → `ControllerBase` migration) is captured here as Rec #35 with a **defer** disposition. Recommend annotating the sibling doc with an "obsolete — see eval doc Rec #35" header rather than deleting it (preserves the design rationale for posterity).
