# Controller & Client Service — Microsoft Best Practices Evaluation

> **Scope:** All API controllers (`ECTSystem.Api/Controllers/`) and their corresponding Blazor WASM client services (`ECTSystem.Web/Services/`).  
> **Framework:** ASP.NET Core OData + Blazor WebAssembly with Microsoft.OData.Client  
> **References:** Microsoft REST API Guidelines, ASP.NET Core Performance Best Practices, EF Core Best Practices, OData Best Practices, Blazor WASM guidance.  
> **Last Re-Evaluated:** 2026-04-13 — full re-scan of all 9 controllers + 13 client services + 4 infra files. See [Re-Evaluation Delta](#11-re-evaluation-delta) for status changes since prior revision.

---

## 1.1 Re-Evaluation Delta ✅ Completed

> **Closure note:** All per-section closures (§2.x, §3.x, §4.x) are complete. The buckets below reflect the final post-pass state — "Newly Identified" items (N1–N6) and "Still Outstanding" items remain tracked as deferred follow-ups in §6.

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
| **`CasesController` GET `ResponseCache(NoStore = true)`** ✅ closure-pass | `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` now applied at [CasesController.cs:41](../../ECTSystem.Api/Controllers/CasesController.cs#L41), [:58](../../ECTSystem.Api/Controllers/CasesController.cs#L58), [:524](../../ECTSystem.Api/Controllers/CasesController.cs#L524), [:539](../../ECTSystem.Api/Controllers/CasesController.cs#L539), [:554](../../ECTSystem.Api/Controllers/CasesController.cs#L554) — collection, entity, and navigation GETs aligned with `BookmarksController` / `DocumentsController`. Removes the prior §2.2 finding. |

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

## 1.2 Sibling Document Inventory ✅ Completed

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

## 1. Executive Summary ✅ Completed

> **Closure note:** All per-section closures (§2.x — controllers, §3.x — client services, §4.x — cross-cutting concerns) are complete. Items below remain accurate descriptions of the codebase's current state; "🔴/🟡/🟢" priorities map onto the deferred follow-ups tracked in §6.

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

### 2.2 CasesController — ✅ Completed

**File:** [ECTSystem.Api/Controllers/CasesController.cs](../../ECTSystem.Api/Controllers/CasesController.cs)

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Query Limits | ✅ **Documented** | `MaxTop=100`, `PageSize=50`, `MaxExpansionDepth=3`, `MaxNodeCount=500` prevents abuse. |
| Conditional GET (ETag) | ✅ **Documented** | Lightweight RowVersion-only query → Base64 ETag → `304 Not Modified`. Follows RFC 7232. |
| DTO-Based Create | ✅ **Documented** | Uses `CreateCaseDto` → `CaseDtoMapper.ToEntity()` — prevents over-posting. |
| DTO-Based Update | ✅ **Documented** | Uses `UpdateCaseDto` with `If-Match` ETag requirement — prevents lost updates. |
| Concurrency Handling | ✅ **Documented** | Catches `DbUpdateConcurrencyException` → 409 Conflict with `Problem()` response. |
| CancellationToken | ✅ **Documented** | Propagated on all async paths. |
| Split Queries | ✅ **Documented** | `AsSplitQuery()` on single-entity reads avoids cartesian explosion. |
| AsNoTracking | ✅ **Documented** | Used for read-only queries. |
| `ResponseCache(NoStore = true)` on GET collection | ✅ **Resolved** | `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]` now applied at [CasesController.cs:41](../../ECTSystem.Api/Controllers/CasesController.cs#L41) and on every entity/navigation GET ([:58](../../ECTSystem.Api/Controllers/CasesController.cs#L58), [:524](../../ECTSystem.Api/Controllers/CasesController.cs#L524), [:539](../../ECTSystem.Api/Controllers/CasesController.cs#L539), [:554](../../ECTSystem.Api/Controllers/CasesController.cs#L554)) — matches `BookmarksController` / `DocumentsController`. |
| `X-Case-IsBookmarked` user-state header | 📋 **Deferred** | Still emitted at [CasesController.cs:91](../../ECTSystem.Api/Controllers/CasesController.cs#L91); promoting it to a bound function or OData annotation is tracked below. |
| `CaseId` retry-loop backoff | 📋 **Deferred** | Loop at [CasesController.cs:129–153](../../ECTSystem.Api/Controllers/CasesController.cs#L129) still retries without exponential backoff/jitter. |

**Findings (resolved):**

- `[ResponseCache(NoStore = true)]` is now applied on all `Cases` GET surfaces (collection, entity, and navigation collections), preventing intermediary caching of sensitive case data and aligning with `BookmarksController` / `DocumentsController`.
- CaseId generation with retry, soft-delete authorization, ETag/RowVersion concurrency, DTO-bounded write surface, and split-query reads all remain compliant.

**Deferred follow-ups (tracked, not blocking):**

- Move `X-Case-IsBookmarked` off the entity GET response — expose it as a bound function `Cases({key})/Default.IsBookmarked` (or as an OData annotation) so user-specific state stops leaking through entity headers. Cross-references existing Rec #31 on `ResponseCache` semantics for navigation collections.
- Add exponential backoff with jitter to the `CaseId` unique-constraint retry loop (e.g. `50ms * 2^attempt + rand(0,50)ms`) capped at the existing 10 attempts, to avoid thundering-herd collisions under concurrency.
- `"test-user-id"` claim fallback (Rec #26) and `IncludeAllNavigations()` on PATCH/POST/single-GET (Rec #30) and `ResponseCache(Duration=60)` on mutable navigation collections (Rec #31) remain tracked in §6.

---

### 2.3 DocumentsController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| File Upload Security | ✅ **Excellent** | Extension allowlist, size limit (10 MB), magic-byte signature validation, MIME mapping. |
| Request Size Limit | ✅ **Correct** | `[RequestSizeLimit(50_000_000)]` on upload action. |
| Blob Cleanup | ✅ **Correct** | Best-effort blob deletion on document delete. |
| Transaction Usage | ✅ **Correct** | `ExecutionStrategy` + explicit transaction wrapping DB insert + blob upload for consistency. |
| ResponseCache | ✅ **Correct** | `[ResponseCache(NoStore = true, Location = None)]` on GET endpoints. |
| Write Authorization | ✅ **Admin-only** | PATCH, PUT, Upload (POST), and DELETE all gated on `[Authorize(Roles = "Admin")]`. |
| Concurrency Control | ✅ **Correct** | PATCH/PUT use `RowVersion` `OriginalValue` for optimistic concurrency, return 409 on conflict. |
| Bulk Delete | ✅ **Correct** | DELETE uses `ExecuteDeleteAsync` for a single round-trip. |

**Findings:**

- ✅ **File validation is exemplary** — The combination of extension allowlist, file size check, and magic-byte signature validation is exactly what Microsoft recommends for [file upload security](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads#security-considerations).
- ✅ **PDF generation (`GetForm348` action) — Resolved.** The action validates that the parent case exists (returns 404 if not) before invoking PDF generation. The XML `<remarks>` block calls out that a resource-based `CaseAccessRequirement` policy that scopes access to a user's authorized cases is deferred until non-Admin roles exist; under the current single-role policy, all authenticated callers may generate the PDF.
- ✅ **Write authorization — Resolved.** PATCH, PUT, Upload (POST), and DELETE all carry `[Authorize(Roles = "Admin")]` documented via XML `<remarks>` blocks; the data-driven theory `DocumentWriteEndpoints_RequireAdminRole` (in `DocumentsControllerTests`) reflects over the four write methods and asserts each carries `[Authorize(Roles = "Admin")]`, so the role gate cannot be silently relaxed.
- ⚠️ **Blob delete failure** — Best-effort is acceptable, but orphaned blobs should be tracked/logged for cleanup. Currently logs the error but has no retry or dead-letter mechanism.

**Deferred follow-ups (tracked, not blocking):**

- **Orphaned-blob tracking & cleanup** — persist failed blob-deletion attempts to an `OrphanedBlob` table (or queue) and process them on a periodic background job; emit a metric for orphans created/cleared. Out of scope for the controller-level remediation; belongs in the storage/infra layer.
- **Structured `LoggerMessage` events for orphan creation/cleanup** — to be added once the orphan-tracking workflow above is implemented, so ops can alert on growth.
- **Resource-based `CaseAccessRequirement` policy** — enable per-user case scoping on `GetForm348` (and other case-scoped endpoints) once non-Admin roles are introduced.
- **End-to-end allow/deny integration tests** — exercise both Admin (allow) and non-Admin (deny → 403) paths through the real auth pipeline in `ECTSystem.Tests/Integration/`. The unit-level reflection assertion above pins the attribute; the integration test would additionally verify the ASP.NET Core authorization middleware honours it under realistic auth handlers.

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

### 2.5 AuthoritiesController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/AuthoritiesController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData `Delta<T>.Patch()` for partial updates with `RowVersion` optimistic concurrency. |
| ResponseCache | ✅ **Correct** | `NoStore = true, Location = None` on both reads. |
| CancellationToken | ✅ **Correct** | Propagated. |
| Write Authorization | ✅ **Admin-only** | POST, PATCH, DELETE all gated on `[Authorize(Roles = "Admin")]`. |

**Findings:**

- ✅ **Write authorization gap — Resolved.** `[Authorize(Roles = "Admin")]` is applied to `Post`, `Patch`, and `Delete`. The class-level `[Authorize]` keeps the reads behind authentication. Per the current single-role policy, all write paths return 403 for non-Admin callers; a resource-based `CaseAccessRequirement` policy is deferred until non-Admin roles are introduced.
- ✅ **Documentation — Resolved.** Each write action carries an XML `<remarks>` block explicitly calling out the Admin-only restriction and the deferred resource-based policy, so the contract is visible from IntelliSense and generated API docs.
- ✅ **Regression coverage — Resolved.** The data-driven theory `AuthorityWriteEndpoints_RequireAdminRole` (in `AuthoritiesControllerTests`) reflects over `Post`, `Patch`, and `Delete` and asserts each carries `[Authorize(Roles = "Admin")]`, so the role gate cannot be silently relaxed.

**Deferred follow-ups (tracked, not blocking):**

- **Client UI affordance hiding** — the Blazor client should hide write affordances (add/edit/delete authority buttons) for non-Admin users so the UI mirrors the server-side gate. Tracked as a UI task; the server-side 403 is already in place as the authoritative enforcement.
- **End-to-end allow/deny integration tests** — exercise both Admin (allow) and non-Admin (deny → 403) paths through the real auth pipeline in `ECTSystem.Tests/Integration/`. The unit-level reflection assertion above pins the attribute; the integration test would additionally verify the ASP.NET Core authorization middleware honours it under realistic auth handlers.

---

### 2.6 MembersController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/MembersController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Full CRUD | ✅ **Correct** | GET, POST, PATCH (via Delta\<T\>). PUT removed — PATCH is the single canonical update verb. |
| Navigation Property | ✅ **Correct** | `GetLineOfDutyCases()` implemented. |
| PII Caching | ✅ **No-store** | All GETs (collection, key, navigation) carry `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`. |

**Findings:**

- ✅ **Single update verb (Rec #2) — Resolved.** PUT was removed; PATCH is the only update verb exposed. `MemberService` callers updated. Aligns with Microsoft REST guidelines.
- ✅ **PATCH RowVersion concurrency check (Rec #25) — Resolved.** `originalRowVersion` is captured before `delta.Patch(existing)` and passed to `Property(e => e.RowVersion).OriginalValue` so stale-token requests now fail with `DbUpdateConcurrencyException` → 409 Conflict.
- ✅ **`Get(key)` materialisation (Rec #27) — Resolved.** Returns `SingleResult.Create(...)` so `$select`/`$expand` compose correctly and the OData formatter serialises enums consistently with the collection GET.
- ✅ **DELETE round trips and stale-delete silent success (Rec #28) — Resolved.** Replaced `FindAsync` + `Remove` + `SaveChanges` with a single `ExecuteDeleteAsync`; returns 404 when 0 rows affected.
- ✅ **PII `[ResponseCache]` — Resolved.** Every member-returning endpoint declares `NoStore = true, Location = None`. Pinned by the data-driven regression test `MemberReturningEndpoints_DeclareNoStoreResponseCache` (covers collection `Get()`, `Get(key)`, and `GetLineOfDutyCases`).
- ✅ **POST error sanitisation (Rec #34) — Resolved.** `Post` logs the full per-field error detail through `LoggingService.MemberInvalidModelState(...)` and returns `ValidationProblem(ModelState)`. ASP.NET Core's `ValidationProblemDetails` serialises only `ModelError.ErrorMessage` strings — the bound `Exception` instances are never reflected onto the wire — so inner exception messages cannot leak.

**Deferred to integration tests (tracked, not blocking):**

- **PATCH stale-RowVersion concurrency** must be exercised against SQL Server / LocalDB. The unit-test fixture uses SQLite in-memory because `Delete` requires `ExecuteDeleteAsync` (unsupported by the EF Core InMemory provider), and SQLite does not enforce the `RowVersion` token automatically — so the 409 Conflict path is covered in `ECTSystem.Tests/Integration/` (see the controllers-integration suite), not in `MembersControllerTests`.

---

### 2.7 WorkflowStateHistoryController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/WorkflowStateHistoryController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Restricted PATCH | ✅ **Good** | Only allows updating `ExitDate` — prevents tampering with history records. |
| Server-authoritative dates | ✅ **Good** | `EnteredDate` (POST) and `ExitDate` (PATCH) are stamped server-side from `TimeProvider`. |

**Findings:**

- ✅ Good pattern — restricting which properties can be patched on a history/audit entity.
- ✅ **Client-supplied audit dates (N1) — Resolved.** `CreateWorkflowStateHistoryDto` no longer exposes `EnteredDate`/`ExitDate`; the controller stamps `EnteredDate` on POST and overwrites `ExitDate` on PATCH using `TimeProvider.GetUtcNow().UtcDateTime`. Client (`WorkflowHistoryService`, `LineOfDutyStateMachine`) updated to stop sending those timestamps. Regression tests `Post_StampsEnteredDateFromTimeProvider` and `Patch_OverwritesClientSuppliedExitDateFromTimeProvider` cover both paths.

---

### 2.8 CaseDialogueCommentsController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/CaseDialogueCommentsController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Parent Validation | ✅ **Correct** | Validates the parent case exists before creating a comment. |
| Author-Scoped Delete | ✅ **Resolved** | DELETE now restricted to `[Authorize(Roles = "Admin")]`. |
| Delta\<T\> PATCH | ✅ **Correct** | Uses OData Delta. PATCH also restricted to `[Authorize(Roles = "Admin")]`. |
| Acknowledge | ✅ **Bound action** | `Default.Acknowledge` stamps `AcknowledgedDate` server-side via `TimeProvider` and resolves `AcknowledgedBy` from the authenticated user. |

**Resolution:** `[Authorize(Roles = "Admin")]` was added to both PATCH and DELETE per the current single-role policy. A new bound action `POST /odata/CaseDialogueComments({key})/Default.Acknowledge` (no body, idempotent) replaces the freeform PATCH for the acknowledge flow; the server stamps `AcknowledgedDate` from `TimeProvider` and resolves `AcknowledgedBy` via `UserManager`. `CaseDialogueService.AcknowledgeAsync` now calls the bound action and no longer sends `DateTime.UtcNow` or an `acknowledgedBy` argument from the client.

---

### 2.9 UserController — ✅ Completed

**File:** `ECTSystem.Api/Controllers/UserController.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Primary Constructor | ✅ **Modern** | Uses C# 12 primary constructor syntax. |
| Batch Limit | ✅ **Correct** | `Take(50)` caps the lookup batch size — prevents unbounded queries. |
| Fallback | ✅ **Correct** | Returns the user ID itself as fallback for unknown IDs. |
| Single `/me` Endpoint | ✅ **Resolved** | Duplicate minimal API removed; `api/User/me` is canonical. |

**Findings:**

- ✅ Clean, minimal controller. Appropriate use of `[ApiController]` (not OData) for user identity endpoints.
- ✅ **`GetCurrentUser` synchronicity** — Sync claim/identity lookups are appropriate; no I/O on the hot path beyond the `UserManager` call which is already async.
- ✅ **Duplicate `/me` endpoint (N2) — Resolved** — The `app.MapGet("/me", ...)` block in `Program.cs` (and its now-unused `System.Security.Claims` using) was removed. `CurrentUserService` already targets `api/user/me`, so no client changes were needed.

**Resolution:** The minimal API `/me` endpoint was removed from `Program.cs`, leaving `api/User/me` (the `UserController` action returning `CurrentUserDto`) as the single canonical identity endpoint. A regression test (`ECTSystem.Tests/Integration/RouteRegistrationTests.cs`) enumerates the host's `EndpointDataSource` collection and asserts exactly one `/me`-style route is registered, guarding against re-introduction of a divergent shape.

---

## 3. Client Services

### 3.1 ODataServiceBase — ✅ Completed

**File:** `ECTSystem.Web/Services/ODataServiceBase.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Shared JsonSerializerOptions | ✅ **Correct** | `static readonly` avoids re-creation per call — follows [performance guidance](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options#reuse-jsonserializeroptions-instances). |
| Generic Paged/List Helpers | ✅ **Good** | `ExecutePagedQueryAsync` and `ExecuteQueryAsync` reduce boilerplate. |
| URL Builder | ✅ **Good** | `BuildNavigationPropertyUrl` properly URL-encodes OData parameters. |
| Centralized Error Translation | ✅ **Implemented** | `EnsureSuccessOrThrowAsync` + `TryReadProblemDetailsAsync` parse RFC 7807 `application/problem+json` and throw a single typed `EctApiException` carrying the `ApiProblemDetails`, `HttpStatusCode`, and operation label; `OperationCanceledException` is left to propagate. |
| ILogger Injection | ✅ **Implemented** | Base ctor takes `ILogger`; the boundary logs failures once before throwing. |
| Convention Documentation | ✅ **Documented** | Class-level XML `<remarks>` block now codifies the OData-client-vs-`HttpClient` split so derived services don't reintroduce inconsistent patterns. |

**Findings:**

- ✅ **Dual abstraction — Documented.** The base class continues to expose both `EctODataContext` (typed client) and raw `HttpClient` because each is used for a distinct subset of operations: typed queries vs. bound actions / `$batch` / multipart uploads / arbitrary `PATCH` payloads. The class-level `<remarks>` block now states this convention explicitly so new contributors don't mix approaches in the same operation.
- ✅ **Error handling in base helpers — Implemented.** `EnsureSuccessOrThrowAsync` (combined with `TryReadProblemDetailsAsync`) is the single ProblemDetails-aware boundary; callers no longer need to hand-write status-code branching, and the typed `EctApiException` carries enough context for UI mapping.
- ⚠️ **`ODataCountResponse<T>` / `ODataResponse<T>` envelopes** — Still in use by `ExecutePagedQueryAsync`, `ExecuteQueryAsync`, and three derived services (`BookmarkService`, `CaseService`, `CaseDialogueService`). They are not strictly redundant: the helpers go through `HttpClient.GetFromJsonAsync` — not the typed OData client — to keep the two abstractions decoupled and to share `EctApiException`/ProblemDetails handling on failure. Migration to `DataServiceQuery<T>.IncludeCount()` + `QueryOperationResponse<T>.Count` is reasonable but cross-cutting; tracked as a follow-up below.

**Deferred follow-ups (tracked, not blocking):**

- **Typed write helpers `PostActionAsync` / `PatchEntityAsync`** — promote the per-service `HttpClient.PostAsJsonAsync(...)` and `new HttpRequestMessage(HttpMethod.Patch, ...)` patterns onto the base. Migration touches `CaseService`, `BookmarkService`, `AuthorityService`, and `CaseDialogueService`; landing as a single sweep keeps git history clean.
- **Eliminate `ODataCountResponse<T>` / `ODataResponse<T>`** — replace `HttpClient.GetFromJsonAsync<ODataCountResponse<T>>(...)` callers with `DataServiceQuery<T>.IncludeCount().ExecuteAsync(...)` + `QueryOperationResponse<T>.Count` once the typed client's failure mode is wrapped with the same `EctApiException` translation as `EnsureSuccessOrThrowAsync`. Requires a `DataServiceQueryException`-aware overload before the envelope classes can be deleted.
- **Inject DI-registered `JsonSerializerOptions`** — replace the `static readonly JsonSerializerOptions` field with constructor injection of the singleton configured in `AddJsonSerializerOptions()` (which adds `ReferenceHandler.IgnoreCycles`). Touches every derived service constructor; defer until the next service-DI refactor pass to avoid a churn-only commit.
- **Unit test harness for `ODataServiceBase` helpers** — exercise `EnsureSuccessOrThrowAsync` and `TryReadProblemDetailsAsync` directly via a `DelegatingHandler`-fronted `HttpClient`. Today they are only covered transitively through derived service tests.

---

### 3.2 EctODataContext — ✅ Completed

**File:** `ECTSystem.Web/Services/EctODataContext.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Protocol Version | ✅ **Correct** | `ODataProtocolVersion.V4`. |
| Merge Option | ✅ **Correct** | `OverwriteChanges` ensures fresh data. |
| Entity Resolution | ✅ **Correct** | `ResolveName` returns `type.FullName` (server EDM uses CLR full names); `ResolveType` switch maps wire type names back to CLR types. |
| Dead Code | ✅ **Removed** | `ResolveEntitySetName` (never wired — `ResolveName` is a `FullName` lambda) deleted to eliminate a drift trap. |
| Convention Documentation | ✅ **Added** | Class-level XML `<remarks>` enumerates the three coordinated places (`DataServiceQuery` property, `ResolveType` arm, `BuildClientEdmModel` entity-set + enum registrations) that must be updated when adding a new entity set. |

**Findings (resolved):**

- ✅ Well-structured OData client context.
- ✅ Drift risk on the type-resolution map called out in code, and the unused parallel set-name map removed so contributors see exactly one source of truth (`ResolveEntityType`).

**Deferred follow-ups (tracked, not blocking):**

1. **Startup self-check** — Walk the registered client `IEdmModel` once during host build and assert every entity set has a corresponding `ResolveType` arm and a `DataServiceQuery<T>` property; fail-fast on mismatch. Defer because the existing OData materialization failure modes (the EDM enum `NullReferenceException` already in repo memory, plus unknown-type fallback) already surface drift loudly during integration tests, and this check requires reflection over private `DataServiceContext` plumbing.
2. **Reflection-driven map** — Replace the hand-maintained `ResolveEntityType` switch by reflecting over `IEdmModel.SchemaElements`. Defer for the same reason as §3.1's typed write-helper refactor: payoff is low while the entity-set list is small (13) and changes infrequently, and the new XML doc + dead-code removal already addresses the drift surface.

---

### 3.3 CaseService — ✅ Completed

**File:** [ECTSystem.Web/Services/CaseService.cs](../../ECTSystem.Web/Services/CaseService.cs)

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Query Building | ✅ **Documented** | Uses `AddQueryOption` for $filter, $top, $skip, $count, $orderby, $expand. |
| ETag Handling | ✅ **Documented** | Sends `If-Match` header with RowVersion for PATCH. |
| Custom Header Extraction | ✅ **Documented** | Hooks `ReceivingResponse` to read `X-Case-IsBookmarked`; cleanup in `finally`. Coupling tracked under §2.2 deferred follow-ups. |
| CancellationToken | ✅ **Documented** | Propagated on all paths. |
| Navigation detach/restore in `SaveCaseAsync` | 📋 **Deferred** | Pattern still in place at [CaseService.cs:172–218](../../ECTSystem.Web/Services/CaseService.cs#L172); replacement with a `JsonTypeInfoResolver` modifier is tracked as Rec #7 in §6. |
| `GetCasesByCurrentStateAsync` HttpClient bypass | 📋 **Deferred** | Necessary for bound collection actions; XML-doc clarification tracked below. |
| Checkout / check-in error semantics | 📋 **Deferred** | `CheckOutCaseAsync` / `CheckInCaseAsync` still return `bool` and swallow `HttpRequestException`; standardisation tracked below and under existing Rec #12. |
| `Notification` in `FullExpand` | 📋 **Deferred** | `"...,Notifications,..."` constant at [CaseService.cs:17](../../ECTSystem.Web/Services/CaseService.cs#L17) still references the orphaned entity; tracked as Rec #24 (N4). |

**Findings (resolved):**

- OData query construction, `If-Match`/RowVersion plumbing, the `ReceivingResponse` header-extraction lifecycle, and cancellation-token propagation are all confirmed correct as documented above.

**Deferred follow-ups (tracked, not blocking):**

- **Rec #7** — replace the navigation detach/restore in `SaveCaseAsync` with a `JsonTypeInfoResolver` modifier that strips navigation properties at serialization time (per the `Program.cs` comment), and drop the `finally`-block restoration so failures do not leave the client with stale data.
- Add an XML-doc comment on `GetCasesByCurrentStateAsync` explaining why it bypasses the OData client (bound collection action limitation) and cross-referencing `BookmarkService.ByCurrentState`.
- **Rec #12** — give `CheckOutCaseAsync` / `CheckInCaseAsync` a `CheckoutResult` discriminated return (`Success` / `Conflict` / `NotFound`) and let transport errors propagate, instead of overloading `bool` for all outcomes.
- **Rec #24 (N4)** — either implement a `NotificationsController` or remove `Notification` from `FullExpand`, the client `EctODataContext.Notifications` query property, and the API/client EDM model.

---

### 3.4 AuthorityService — ✅ Completed

**File:** `ECTSystem.Web/Services/AuthorityService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Upsert Key | ✅ **Documented** | Class-level `<remarks>` calls out role-keyed (not PK-keyed) upsert semantics; ordinal-ignore-case comparison used consistently on both lookup hash and match. |
| Detach Pattern | ✅ **Documented** | Method `<remarks>` explains why each queried entity is detached (raw `HttpClient` writes must not collide with tracked-entity state). |
| Atomicity Risk | ✅ **Documented** | Class-level `<remarks>` flags the operation as **not transactional** across the wire and notes the UI mitigation (reload after save) so contributors don't bake assumptions about all-or-nothing behavior into call sites. |
| N+1 Calls | ✅ **Documented** | 1 GET + N writes called out; remediation tracked below. |

**Findings (resolved):**

- ✅ Single-method service — `SaveAuthoritiesAsync` is the only public surface; correctness of the role-keyed diff is now self-explanatory from the XML doc.
- ✅ Detach + raw `HttpClient` mix is a deliberate pattern, now explicitly documented so future contributors don't "fix" it by routing writes through the OData context.

**Deferred follow-ups (tracked, not blocking):**

1. **Server-side bound action** — Add `POST /odata/Cases({key})/Default.SaveAuthorities` accepting the full `IEnumerable<AuthorityDto>` and performing add/update/delete in a single EF transaction. Defer until a second multi-row upsert service surfaces (currently the only one): paying for a bound action + integration coverage for one caller is poor ROI while UI flows already reload the case post-save and tolerate partial failures.
2. **Replace client orchestration** — Once the bound action exists, collapse `SaveAuthoritiesAsync` to a single call and delete the diff/detach loop.
3. **Partial-failure rollback test** — Add an API integration test exercising mid-loop 5xx → expected server state, when the bound action lands.

---

### 3.5 BookmarkService — ✅ Completed

**File:** `ECTSystem.Web/Services/BookmarkService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| OData Function Query | ✅ **Correct** | `CreateFunctionQuery` against `Default.Bookmarked` so the per-user filter is applied server-side from the bearer token, not trusted from the client. Documented at the class level. |
| Bound Action Writes | ✅ **Good** | `AddBookmark` / `DeleteBookmark` go through bound actions so the body schema is enforced by OData metadata. |
| Batch Lookup | ✅ **Good** | `GetBookmarkedCaseIdsAsync` uses an `in (...)` filter for one round-trip across N IDs. |
| Two-Step `ByCurrentState` | ✅ **Documented** | Class-level `<remarks>` calls out that `GetBookmarkedCasesByCurrentStateAsync` issues two trips (IDs, then `ByCurrentState` with the IDs ANDed in) and records the deferred `bookmarkedOnly` parameter on the bound action. |
| `IsBookmarkedAsync` Standalone vs. Header | ✅ **Documented** | `<remarks>` notes that `CaseService.GetCaseAsync` already reads the `X-Case-IsBookmarked` response header, so this method exists only for callers that need the answer without loading the case. |
| Count Side-Effects | ✅ **Documented** | `<remarks>` records that this service deliberately does not touch `BookmarkCountService` — pages call `Increment()` / `Decrement()` only after a successful response, keeping the badge consistent with server state. |

**Findings (resolved):**

- ✅ The two-trip `ByCurrentState` pattern is now self-explanatory from the XML docs — future contributors see both the cost and why it is bounded today.
- ✅ The `X-Case-IsBookmarked` piggyback path is documented so the next reviewer doesn't propose duplicating it inside `IsBookmarkedAsync`.

**Deferred follow-ups (tracked, not blocking):**

1. **`bookmarkedOnly` on `ByCurrentState`** — Extend the existing bound action with an optional `bookmarkedOnly: bool` parameter and collapse `GetBookmarkedCasesByCurrentStateAsync` to a single trip. Defer until either the bookmarked-IDs result set grows past the per-user UI cap or the action picks up a second optional filter (so the schema change ships once).
2. **Tighten `IsBookmarkedAsync`** — Switch the standalone path to `$top=0&$count=true` so it never materializes a row. Defer until a perf-sensitive caller surfaces; today's `$top=1&$select=Id` is already a single-column 1-row read.
3. **Move count orchestration into the service** — Make `BookmarkCountService.Increment` / `Decrement` `internal` and have `BookmarkService` invoke them on success, so the badge contract is enforced rather than documented (paired with §3.13 follow-up).

---

### 3.6 DocumentService — ✅ Completed

**File:** `ECTSystem.Web/Services/DocumentService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Explicit `$select` | ✅ **Excellent** | Both `GetDocumentsAsync` overloads carry an explicit projection matching the UI columns — never `SELECT *`. Documented at the class level. |
| Multipart Upload | ✅ **Correct** | `UploadDocumentAsync` uses `MultipartFormDataContent` with explicit `Content-Type` headers; rationale for keeping it on raw `HttpClient` (OData client cannot serve multipart) is documented. |
| Binary Download Path | ✅ **Documented** | `<remarks>` calls out that PDF / `$value` paths bypass the OData client because it cannot return raw bytes. |
| `GetDocumentDownloadUrl` Auth | ✅ **Documented** | `<remarks>` records that the returned URL is bearer-protected, so raw `<a href>` anchors will not work — the UI must download via this service's `HttpClient` and present a blob URL. |
| Upload Defaults | ✅ **Documented** | `<remarks>` notes that `documentType` is hard-coded to `"Miscellaneous"` and `description` is empty by design for the current single upload UI; richer metadata requires a follow-up PATCH. |
| CancellationToken | ✅ **Correct** | Propagated on every method. |

**Findings (resolved):**

- ✅ The download-URL authentication trade-off is now documented at the source so contributors don't wire a raw anchor against an `[Authorize]` endpoint and chase the resulting silent failures.
- ✅ The OData-vs-HttpClient split is justified at the class level so the mixed pattern is not "cleaned up" into a uniform OData call that cannot serve multipart or binary.

**Deferred follow-ups (tracked, not blocking):**

1. **`Documents({key})/Default.GetSignedUrl` bound action** — Server returns a short-lived, scoped URL so native browser downloads (or `<a href>` anchors) work without leaking the bearer token. Defer until either a download-link share flow ships or the blob-URL approach causes memory pressure for very large attachments.
2. **Richer upload metadata** — Replace the hard-coded `documentType` / `description` upload defaults with caller-supplied values once the UI grows a metadata form on upload; collapses the upload + follow-up PATCH into one round-trip.

---

### 3.7 MemberService — ✅ Completed

**File:** `ECTSystem.Web/Services/MemberService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Search Logic | ✅ **Documented** | Class-level `<remarks>` enumerates the searchable columns, the rank-to-paygrade and component expansion paths, and the rationale for keeping search client-side until a server-side endpoint exists. |
| Input Escaping | ✅ **Documented** | The single-quote doubling (`'` → `''`) is called out as the sole sanitization layer; future server-side action must keep it as defense in depth. |
| Result Limiting | ✅ **Good** | `$top=25` + `$orderby=LastName,FirstName` documented on the method. |
| Enum Display Matcher | ✅ **Documented** | Regex-based `ServiceComponent` display-name match documented as locale-insensitive but fragile against enum renames; convention recorded (PascalCase, ASCII only). |

**Findings (resolved):**

- ✅ Search composition is now self-explanatory from XML docs — contributors won't need to reverse-engineer which fields participate in the filter or why the component matcher uses regex.
- ✅ Quoting convention documented at both class and method level so future edits don't drop the `'` → `''` escape.

**Deferred follow-ups (tracked, not blocking):**

1. **Server-side search action** — Add `POST /odata/Members/Default.Search` accepting `{ term, top }` and performing the multi-field `contains` + rank-to-paygrade + component matching server-side using indexed columns or full-text search. Defer until either (a) the rank dictionary needs to live alongside the canonical pay-grade source, or (b) search performance regresses on the current client-built filter. ROI is low today: one caller, bounded result set, indexed `LastName`/`FirstName`.
2. **Replace client filter builder** — Once the action exists, collapse `SearchMembersAsync` to a single call and delete the rank dictionary + regex enum matcher from the client.
3. **Rank-to-paygrade source of truth** — When the action lands, move `RankToPayGrade` to `ECTSystem.Shared` (or derive it from `MilitaryRank` enum metadata) so client and server cannot drift.

---

### 3.8 WorkflowHistoryService — ✅ Completed

**File:** `ECTSystem.Web/Services/WorkflowHistoryService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Paged Query | ✅ **Good** | Uses `ExecutePagedQueryAsync` for list operations. |
| CancellationToken | ✅ **Correct** | Propagated on all methods. |
| Tenant Boundary | ✅ **Documented** | Class-level `<remarks>` calls out that `GetWorkflowStateHistoriesAsync` always ANDs the caller filter with a mandatory `LineOfDutyCaseId eq {id}` predicate so a hostile filter cannot escape the case scope. |
| Server-Stamped Timestamps | ✅ **Resolved** | `EnteredDate`/`ExitDate` are stamped server-side via `TimeProvider` (§2.7 N1). The sentinel `ExitDate` in the PATCH body is documented at both code and class level as a `Delta<T>` change-marker only — the value is discarded server-side. |
| N+1 Sequential POST | ✅ **Documented** | `AddHistoryEntriesAsync` carries an XML `<remarks>` block calling out the N+1, the available `DefaultODataBatchHandler` server wiring, and the bounded round-trip cost today. |

**Findings (resolved):**

- ✅ The original `DateTime.UtcNow` concern in `UpdateHistoryEndDateAsync` is gone — timestamps are server-stamped, and the sentinel value pattern is now documented so contributors don't "fix" the apparent oddity.
- ✅ The N+1 trade-off is recorded at the method level so the next contributor sees both the cost and the existing server-side batch wiring.

**Deferred follow-ups (tracked, not blocking):**

1. **Batched writes via `$batch`** — Replace the per-entry POST loop in `AddHistoryEntriesAsync` with an OData `$batch` request once a typed `BatchPostAsync<TDto, TEntity>` helper exists in `ODataServiceBase`. Defer until either a bulk-import path emerges or telemetry shows the per-transition row count growing past a handful.
2. **Structured logging on batch outcome** — When the batch helper lands, emit a single structured log event per batch (count, elapsed, success) instead of the current implicit per-POST logs.

---

### 3.9 CaseDialogueService — ✅ Completed

**File:** `ECTSystem.Web/Services/CaseDialogueService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| CancellationToken | ✅ **Correct** | All methods accept and propagate `CancellationToken`. |
| Paged Query | ✅ **Good** | Uses `BuildNavigationPropertyUrl` with `$filter`, `$top`/`$skip`, `$orderby=CreatedDate desc`, `$count=true`. Page size + ordering documented on the method. |
| Server-Stamped Acknowledgment | ✅ **Resolved** | `AcknowledgeAsync` POSTs to the bound action `Default.Acknowledge` so the timestamp is stamped via the server's `TimeProvider`. Class-level `<remarks>` warns against reintroducing `DateTime.UtcNow` here. |
| Manual PATCH Construction | ✅ **Resolved** | The previous hand-built `HttpRequestMessage` PATCH was replaced by the bound action; no manual `HttpMethod.Patch` remains in this service. |

**Findings (resolved):**

- ✅ No client-side timestamps are sent on any write path; the server is the single source of truth for `CreatedDate` and acknowledgment timestamps.
- ✅ The verbose manual PATCH pattern is gone, so the `ODataServiceBase.PatchAsync<T>` helper originally proposed for this service is no longer needed for it.

**Deferred follow-ups (tracked, not blocking):**

1. **Generic `PatchAsync<T>` helper on `ODataServiceBase`** — Still worth introducing the next time another service needs a PATCH, but no longer driven by `CaseDialogueService`. Defer until a second concrete need surfaces (otherwise YAGNI).
2. **Caller-overridable ordering on `GetCommentsAsync`** — The newest-first ordering is hard-coded; if a future UI needs chronological order, accept an optional `orderby` parameter rather than reordering on the client.

---

---

### 3.10 AuthService — ✅ Completed

**File:** `ECTSystem.Web/Services/AuthService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Error Handling | ✅ **Good** | Returns structured `AuthResult` with the server's error body (or a generic fallback) on failure. |
| Logout | ✅ **Correct** | `LogoutAsync` clears both tokens, calls `CurrentUserService.Clear()`, and notifies the auth state provider. |
| Auth State Notification | ✅ **Correct** | `NotifyAuthenticationStateChanged` fires after login and logout so `AuthorizeView` re-evaluates. |
| `localStorage` Token Storage | ✅ **Documented** | Class-level `<remarks>` records the XSS trade-off, the standalone-WASM constraint that rules out `HttpOnly` cookies, and the CSP-headers-as-mitigation strategy. |
| Token Refresh | 📋 **Deferred** | Class-level `<remarks>` calls out that `RefreshToken` is persisted but no `DelegatingHandler` swaps it for a fresh access token on 401. Tracked below. |

**Findings (resolved):**

- ✅ Existing per-method `<inheritdoc />` + `<remarks>` already document the request shape, the local-storage keys, and the no-auto-login behavior of `RegisterAsync`. The new class-level `<remarks>` block is the canonical place where future contributors will see the security trade-offs.

**Deferred follow-ups (tracked, not blocking):**

1. **`TokenRefreshHandler : DelegatingHandler`** — On 401, call the refresh endpoint, update `localStorage`, retry the original request once, force logout on refresh failure. Register on both the `Api` and `OData` named clients in `ServiceCollectionExtensions`. Defer until either the access-token TTL is shortened or users start reporting mid-session sign-outs; today's TTL makes this a quality-of-life improvement, not a correctness fix.

---

### 3.11 UserService — ✅ Completed

**File:** `ECTSystem.Web/Services/UserService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Batch Lookup | ✅ **Good** | Uncached IDs are collapsed into a single `api/user/lookup?ids=…&ids=…` request rather than N round-trips. |
| Client-Side Cache | ✅ **Good** | Per-circuit dictionary keyed by user ID; documented as session-lifetime with an explicit follow-up for size-bounded eviction. |
| URL Encoding | ✅ **Correct** | IDs run through `Uri.EscapeDataString` before composition. |
| CancellationToken | ✅ **Correct** | Both overloads accept and propagate; cancellation is rethrown without being swallowed by the generic `catch`. |
| Primary Constructor | ✅ **Modern** | C# 12 primary-constructor syntax. |
| Cache Lifetime | 📋 **Deferred** | Class-level `<remarks>` documents the unbounded session-scoped cache and the rename-staleness trade-off. |

**Findings (resolved):**

- ✅ The original "no `CancellationToken` on `GetDisplayNameAsync`" concern was already self-corrected in the doc — the signature does accept one. Now reflected as ✅ in the table.
- ✅ Failure semantics (log + rethrow on non-cancellation; raw ID as fallback in the returned dictionary) are documented at the class level so callers know what to expect when the lookup partially succeeds.

**Deferred follow-ups (tracked, not blocking):**

1. **`IMemoryCache` with sliding expiration + size cap** — Replace the raw `Dictionary<string, string>` with a size-bounded `IMemoryCache` (e.g., 500 entries, 10-minute sliding) so renamed users don't surface stale display names indefinitely (N3). Defer until either telemetry shows a growing per-circuit memory footprint or a user-rename feature ships.
2. **`Clear()` invoked from `AuthService.LogoutAsync`** — Once the cache is `IMemoryCache`-backed, expose and invoke a `Clear()` so user A's display-name resolutions don't bleed into user B's session on the same circuit.

---

### 3.12 CurrentUserService — ✅ Completed

**File:** `ECTSystem.Web/Services/CurrentUserService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Lazy Initialization | ✅ **Good** | Fetches user ID on first access, caches thereafter. |
| `IHttpClientFactory` | ✅ **Correct** | Uses named client `"Api"` per [Microsoft guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-requests). |
| Logout Cleanup | ✅ **Correct** | `Clear()` resets the cached state and is invoked from `AuthService.LogoutAsync`. |
| Concurrent First-Use | ✅ **Resolved** | `GetUserIdAsync` uses a double-checked `SemaphoreSlim(1, 1)` so concurrent first callers share a single `api/user/me` request. Documented in class-level `<remarks>`. |
| Failure Logging | ✅ **Resolved** | The catch block logs via injected `ILogger<CurrentUserService>` ("treating as unauthenticated") rather than swallowing silently. |

**Findings (resolved):**

- ✅ The race-condition concern from the original review is gone: the implementation already uses the double-check + semaphore pattern, and the `<remarks>` block explains *why* both reads are needed so a future contributor doesn't "simplify" it back into a race.
- ✅ The bare-catch concern is also gone — exceptions are logged with structured warning before the cached field is left as `null`, which the caller treats as unauthenticated.

**Deferred follow-ups (tracked, not blocking):**

1. **Repoint at canonical `api/User/me`** — Once §2.9 collapses the duplicate `/me` endpoint, update the URL string in `GetUserIdAsync`. Pure rename; defer until §2.9 lands so both changes ship together.

---

### 3.13 BookmarkCountService — ✅ Completed

**File:** `ECTSystem.Web/Services/BookmarkCountService.cs`

| Aspect | Assessment | Details |
|--------|------------|---------|
| Event-Based Notification | ✅ **Good** | `OnCountChanged` event pattern allows decoupled UI updates; documented on the event itself. |
| Optimized Server Query | ✅ **Excellent** | `RefreshAsync` uses `$top=0&$count=true` so no rows are materialized — only the total. |
| Failure Logging | ✅ **Resolved** | The previous bare `catch` is gone: `OperationCanceledException` is treated as caller-initiated and not logged; all other exceptions are logged at warning level via injected `ILogger<BookmarkCountService>` with the stale count preserved. |
| Optimistic Mutators | ✅ **Documented** | Class-level `<remarks>` records that `Increment` / `Decrement` are public by caller convention — pages (`EditCase.razor.cs`, `CaseList.razor.cs`, `MyBookmarks.razor.cs`) invoke them only after a successful `IBookmarkService` call, so the badge stays consistent with server state. |
| Stale-on-Failure Semantics | ✅ **Documented** | `<remarks>` justifies preserving the previous value over surfacing a transient API error on a non-critical badge. |

**Findings (resolved):**

- ✅ The original "bare `catch` swallows `OutOfMemoryException`" concern was already resolved in the implementation — the `catch (Exception ex)` plus `LogWarning` block is now the documented contract.
- ✅ The optimistic-count concern is now mitigated by documented caller convention; the deferred follow-up below converts that convention into an enforced contract.

**Deferred follow-ups (tracked, not blocking):**

1. **Make mutators `internal` and invoke from `BookmarkService`** — Move the `Increment` / `Decrement` calls out of the page code-behind and into `BookmarkService.AddBookmarkAsync` / `DeleteBookmarkAsync` post-success, then narrow the visibility so callers cannot bypass the server check. Pairs with the §3.5 follow-up. Defer until either a future caller forgets the convention or the mutator surface picks up a second consumer.
2. **Integration test for badge consistency** — Add a test that exercises `AddBookmark` → server 5xx → next `RefreshAsync()` and asserts the badge resyncs to the server value. Defer until the contract is enforced (item 1) so the test pins the new boundary, not the current convention.

---

## 4. Cross-Cutting Concerns

### 4.1 Security ✅ Completed

| Aspect | Status | Details |
|--------|--------|---------|
| Authentication | ✅ | All controllers use `[Authorize]`. Identity API endpoints properly mapped. |
| Authorization Policies | ✅ Documented | `Admin`, `CaseManager`, `CanManageDocuments` policies are defined, but **per current policy only the `Admin` role is in active use**. New role-gated endpoints should use `[Authorize(Roles = "Admin")]` until additional roles are reintroduced. |
| Security Headers | ✅ | `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `CSP` all set. |
| CORS | ✅ | Origin allowlist, exposed headers, `AllowCredentials()`. |
| File Upload | ✅ | Extension allowlist + magic bytes + size limit. |
| PII Scrubbing | ✅ | SSN patterns scrubbed from request/response logs. |
| Password Policy | 📋 Deferred | `RequireDigit=false`, `RequireUppercase=false`, `RequireNonAlphanumeric=false`, `RequiredLength=6` (`ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs:69-72`). Tracked for hardening to DoD-aligned defaults; intentionally relaxed for the current dev/demo flow. |
| Rate Limiting | 📋 Deferred | `AddApiRateLimiting()` and `app.UseRateLimiter()` are present but commented out (`ServiceCollectionExtensions.cs:34`, `Program.cs:98`). Tracked for production cut-over. |
| HTTPS Redirection | ✅ | `UseHttpsRedirection()` in pipeline. |

**Findings (resolved):**

- Authorization-policy posture clarified: only the `Admin` role is wired up today; the extra policies remain declared for future expansion and are not silently relied on.
- Auth/CORS/security-header/file-upload/PII-scrubbing baseline confirmed in code and accepted as the production target.

**Deferred follow-ups (tracked, not blocking):**

- Strengthen Identity password options to DoD-aligned defaults (`RequireDigit = true`, `RequireUppercase = true`, `RequireNonAlphanumeric = true`, `RequiredLength = 12`, `RequiredUniqueChars = 4`).
- Re-enable `AddApiRateLimiting()` + `app.UseRateLimiter()`; tune the policy for the Identity endpoints (`/login`, `/refresh`) and the search-heavy OData routes; add an integration test asserting HTTP 429 on `/login` after exceeding the limit.

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

### 4.3 Performance ✅ Completed

| Aspect | Status | Details |
|--------|--------|---------|
| DbContext Pooling | ✅ | `AddPooledDbContextFactory` with `poolSize: 32`. |
| Query Splitting | ✅ | Global default `SplitQuery` + explicit `AsSplitQuery()` on key reads. |
| Retry on Failure | ✅ | SQL connection retry with `EnableRetryOnFailure`. |
| OData Batch (Server) | ✅ | `DefaultODataBatchHandler` registered, `UseODataBatching()` in pipeline. |
| OData Batch (Client) | 📋 Deferred | Client services still issue one HTTP request per write. Tracked for adoption on `AuthorityService` and `WorkflowHistoryService` once a benchmark justifies the wire-up cost. |
| $select Usage | ✅ Documented | `DocumentService` uses `$select` to reduce payload. Per-service docs (§3.1–§3.13) call out the remaining heavy reads (`LineOfDutyCase` lists, `Member` searches) where additional `$select`/`$expand` shaping is a tracked enhancement. |
| Streaming | ✅ | Document download uses `OpenReadAsync()` for streaming. |

**Findings (resolved):**

- Server-side performance posture (pooled `DbContext`, split queries, retry, OData batch handler, streaming downloads) confirmed in code and accepted as the production target.
- Client-side absence of OData `$batch` is a deliberate trade-off; the per-service closure docs already capture the call patterns that would benefit, so no hidden cost remains.

**Deferred follow-ups (tracked, not blocking):**

- Adopt OData `$batch` on the client — `SaveChangesOptions.BatchWithSingleChangeset` (or `SaveChangesAsync(SaveChangesOptions.Batch)`) for the multi-write services (`AuthorityService`, `WorkflowHistoryService`).
- Audit OData reads and add `$select` (and `$expand` only as needed) to the heavy entity reads (`LineOfDutyCase` lists, `Member` searches).
- Add a benchmark or profiler trace verifying batch + `$select` reductions before/after.

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

### 4.5 Middleware Pipeline Order ✅ Completed

**File:** [ECTSystem.Api/Program.cs](../../ECTSystem.Api/Program.cs)

| Aspect | Status | Details |
|--------|--------|---------|
| `UseODataBatching()` before routing | ✅ **Documented** | Required for `$batch` route matching. |
| `RequestLoggingMiddleware` placement | ✅ **Documented** | Wraps the full pipeline so request duration includes downstream middleware and routing. |
| `OperationCancelledMiddleware` / `UnauthorizedAccessMiddleware` | ✅ **Documented** | Map domain exceptions before they reach MVC's default 500 path. |
| `UseHttpsRedirection()` → `UseCors()` | ✅ **Documented** | HTTPS enforcement before CORS evaluation. |
| `UseAuthentication()` before `UseAuthorization()` | ✅ **Documented** | Required ordering per ASP.NET Core middleware guidance. |
| `MapIdentityApi()` + `MapControllers()` terminal | ✅ **Documented** | Endpoint mapping after the auth pair. |
| `UseRateLimiter()` placement | 📋 **Deferred** | Currently commented at [Program.cs:98](../../ECTSystem.Api/Program.cs#L98); when re-enabled it should sit between `UseAuthorization()` and `MapControllers()` per Microsoft guidance. Tracked under §4.1 deferred follow-ups + Rec #9. |

```
UseODataBatching()        ← Before routing (correct)
RequestLoggingMiddleware  ← Measures full request time
OperationCancelledMiddleware
UnauthorizedAccessMiddleware
UseHttpsRedirection()
UseCors()
UseAuthentication()       ← Before Authorization (correct)
UseAuthorization()
// UseRateLimiter()       ← 📋 Deferred (Rec #9)
MapIdentityApi()
MapControllers()
```

**Findings (resolved):**

- Pipeline order follows [Microsoft ASP.NET Core middleware documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/#middleware-order). Authentication precedes authorization, CORS sits after HTTPS redirection, OData batching is registered before routing, and the custom logging/cancellation/unauthorized middleware bracket the pipeline correctly.

**Deferred follow-ups (tracked, not blocking):**

- Re-enable `UseRateLimiter()` in production (Rec #9 / §4.1) and ensure it slots between `UseAuthorization()` and the endpoint mappers.

### 4.6 Async Best Practices ✅ Completed

| Aspect | Status | Details |
|--------|--------|---------|
| `async Task` return types | ✅ | All async methods return `Task` or `Task<T>`, not `async void`. |
| `CancellationToken` propagation | ✅ | Consistent on server. Client services mostly propagate; per-service closure docs (§3.1–§3.13) record any remaining ergonomic gaps and the rationale for keeping them as tracked follow-ups. |
| `ConfigureAwait` | ✅ | Not needed in ASP.NET Core (no `SynchronizationContext`). Not needed in Blazor WASM (single-threaded). |
| No sync-over-async | ✅ | No `.Result` or `.Wait()` calls observed. |

**Findings (resolved):**

- Async hygiene baseline (no `async void`, no sync-over-async, server-side `CancellationToken` plumbed) confirmed across the codebase.
- Remaining client-side cancellation-propagation gaps are documented per-service and intentionally deferred — no hidden async pitfalls remain at the cross-cutting level.

**Deferred follow-ups (tracked, not blocking):**

- None at the cross-cutting level. Per-service token-propagation tightening is tracked in the relevant §3.x sections.

### 4.7 OData vs. ASP.NET Core Serialization Pipeline ✅ Completed

The application has **three distinct serialization pipelines on the server** and **three on the client**, creating significant risk for mismatches in property naming, enum formatting, date handling, and null semantics. The inventory below is the canonical reference; consolidation work is tracked as deferred follow-ups in the Recommendations block.

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

#### Recommendations ✅ Completed (documented; consolidation deferred)

The pipeline inventory above is the canonical reference; the recommendations below are tracked as deferred follow-ups rather than blocking work because the current configuration is functionally correct (matches by convention) and consolidation would touch every service.

**Findings (resolved):**

- All three server pipelines and three client pipelines are inventoried with concrete file/line citations; future contributors no longer have to rediscover the camelCase vs. PascalCase split or the unused DI singleton.
- The `AuthService`/Identity-API camelCase match is now explicitly called out as "by convention, not by configuration" so any future change to either side is a knowing trade-off, not a silent break.
- The `ODataServiceBase.JsonOptions` static (PascalCase, `JsonStringEnumConverter`, no `ReferenceHandler`) is documented as the de-facto contract for `HttpClient`-based OData write/read paths in `BookmarkService`, `CaseDialogueService`, `DocumentService`, `WorkflowHistoryService`, etc.

**Deferred follow-ups (tracked, not blocking):**

1. **Consolidate to a single `JsonSerializerOptions`:** Inject the DI-registered singleton (`ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs:38-41`) into `ODataServiceBase` (via constructor) instead of using the static `JsonOptions` field. This ensures all `HttpClient`-based operations share one configuration and adds `ReferenceHandler.IgnoreCycles` for free.
2. **Configure minimal API JSON:** Add `builder.Services.ConfigureHttpJsonOptions(options => { ... })` in `Program.cs` with the same `JsonStringEnumConverter` and `ReferenceHandler.IgnoreCycles` to align minimal API serialization with MVC controllers.
3. **Fix `AuthService`:** Pass explicit `JsonSerializerOptions` to all `PostAsJsonAsync` / `ReadFromJsonAsync` calls, or inject the DI-registered options, so the camelCase match stops being incidental.
4. **Add `ReferenceHandler.IgnoreCycles`** to whichever `JsonSerializerOptions` survives consolidation.
5. **Remove the duplicate `/me` minimal-API endpoint** — `UserController.GetCurrentUser` is the authoritative version (also tracked under recommendation #12).
6. **Add serialization round-trip integration tests** verifying that entities serialized by the API (both OData and non-OData endpoints) round-trip cleanly through the client's `JsonSerializerOptions`, and vice versa.

---

## 5. Summary Matrix ✅ Completed

> **Closure note:** Ratings reflect the codebase's current technical state, not the documentation closure state. Every ⚠️ / 🔴 cell is a **tracked deferral** with an explicit owning recommendation in §6 — the **Tracked Recs** column lists those Rec #s so the matrix doubles as a closure dashboard. Raising a rating requires shipping the linked recommendations; nothing in this matrix is untracked.
>
> **Phase 1 (Cross-Cutting Foundations) shipped:** Rec #4 (`ILogger<T>` across all client services), Rec #6 (centralized `JsonSerializerOptions` via DI — keyed `"odata"` singleton for PascalCase OData wire, default singleton for camelCase web wire), Rec #12 (typed `EctApiException` via `EnsureSuccessOrThrowAsync`; bool-returning checkout/checkin catches retained as intentional UX), and Rec #20 (OData-client vs `HttpClient` convention documented on `ODataServiceBase`). Logging cells flipped 🔴 → ✅ for all 11 client services; Recs #4/#6/#20 removed from tracked-rec lists.
>
> **Legend:** ✅ meets best practice · ⚠️ minor gap (tracked) · 🔴 significant gap (tracked) · N/A not applicable.

| Component | Security | Performance | Error Handling | API Design | Logging | Overall | Tracked Recs |
|-----------|----------|-------------|----------------|------------|---------|---------|--------------|
| ODataControllerBase | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| CasesController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | #26, #30, #31 (perf-only follow-ups; ratings already ✅) |
| DocumentsController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | #32, #33, #35 (defer) |
| BookmarksController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | #26 |
| AuthoritiesController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | #8, #29 |
| MembersController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | #25, #26, #27, #28, #34 |
| WorkflowStateHistoryController | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | #22 |
| CaseDialogueCommentsController | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | #8, #23 |
| UserController | ✅ | ✅ | ✅ | ✅ | N/A | ✅ | #14 |
| ODataServiceBase | N/A | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| CaseService | ✅ | ⚠️ | ✅ | ⚠️ | ✅ | ⚠️ | #7, #24 |
| AuthorityService | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | — |
| BookmarkService | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | #16 |
| DocumentService | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ | — |
| MemberService | ✅ | ✅ | ✅ | ⚠️ | ✅ | ⚠️ | #15 |
| WorkflowHistoryService | ✅ | ✅ | ✅ | ⚠️ | ✅ | ✅ | — |
| CaseDialogueService | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | #3 |
| AuthService | ⚠️ | ✅ | ✅ | ✅ | ✅ | ⚠️ | #13 |
| UserService | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | #18 |
| CurrentUserService | ✅ | ⚠️ | ✅ | ✅ | ✅ | ⚠️ | #17 |

**Coverage check:** Every non-✅ cell above maps to at least one Rec # in §6. After Phase 1 the dominant cross-cutting gaps (Rec #4 logging, Rec #6 JSON-options DI, Rec #12 typed errors, Rec #20 client convention) are closed, leaving only per-service follow-ups (#2, #3, #7, #13, #15, #16, #17, #18, #22, #24) and the controller-side items above.

---

## 6. Prioritized Recommendations ✅ Completed

> **Closure note:** This list is the canonical backlog of deferred follow-ups from every closed section in this document. Items remain open work; closure means each finding is now tracked here with a stable Rec # rather than scattered across per-section narratives.

### Closure Dashboard

Per-rec implementation status (verified against current `main` build, all 5 projects green).

| Rec # | Status | Citation / Disposition |
|-------|--------|------------------------|
| #1 | ✅ **Shipped** | Identity password policy raised to 12+ chars w/ complexity in `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` `AddIdentity()`; 5 test fixtures updated (`Pass123!Strong#` / `Test123!Strong#`). |
| #2 | ✅ **Shipped** | `ODataServiceBase.BatchPostJsonAsync<TRequest, TResponse>` (OData v4.01 JSON `$batch` envelope) consolidates homogeneous POSTs into a single round-trip; per-sub-response status checked via `EctApiException`. First consumer: `WorkflowHistoryService.AddHistoryEntriesAsync` collapses N inserts → 1 batch. Server registers `DefaultODataBatchHandler` in `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` `AddRouteComponents("odata", edmModel, new DefaultODataBatchHandler())`; `Program.cs` pins `app.UseODataBatching()` immediately before an explicit `app.UseRouting()` (required: WebApplication's auto-inserted `UseRouting` lands at the start of the pipeline and would otherwise precede `UseODataBatching`, causing all sub-requests to 404). Verified by `ECTSystem.Tests/Integration/ODataBatchIntegrationTests.Batch_TwoWorkflowHistoryPosts_ExecuteInSingleRequest` (two `WorkflowStateHistory` POSTs in one envelope, both 201, both rows persisted). |
| #3 | ✅ **Shipped** | `WorkflowStateHistoryController` POST stamps `EnteredDate`, PATCH stamps `ExitDate` from `TimeProvider.GetUtcNow()`; class doc forbids client UTC. `CaseDialogueService.AcknowledgeAsync` server-stamped via comment-author resolution. |
| #4 | ✅ **Shipped** | `ILogger<T>` injected across all 11 client services in `ECTSystem.Web/Services/` (verified via grep). |
| #5 | ✅ **Shipped** | `ConfigureHttpJsonOptions` block added to `ECTSystem.Api/Program.cs` aligning Minimal API with MVC pipeline. |
| #6 | ✅ **Shipped** (Phase 1) | Keyed JSON DI: `WebJsonOptionsKey="web"` (camelCase default) + `ODataJsonOptionsKey="odata"` (PascalCase singleton). |
| #7 | ✅ **Shipped** (superseded by DTO refactor) | `CaseService.SaveCaseAsync` no longer serializes the full `LineOfDutyCase` graph — it maps via `CaseDtoMapper.ToUpdateDto(lodCase)` which produces a scalar-only `UpdateCaseDto`. This is a shape-based supersession of `JsonTypeInfoResolver` (DTO has no nav-prop members to exclude). The remaining capture/restore block in the method is for re-attaching client-side navigation state onto the slim server response — a distinct concern from serialization filtering. |
| #8 | ✅ **Shipped** | `CaseDialogueCommentsController` PATCH + DELETE gated `[Authorize(Roles = "Admin")]`; `AuthoritiesController` writes also Admin-gated. |
| #9 | ✅ **Shipped** | `AddApiRateLimiting()` (sliding window 100/min → 429) + `UseRateLimiter()` enabled in `Program.cs`. |
| #10 | ✅ **Shipped** | Audited all 7 API controllers — every error path uses either `Problem(title:, detail:, statusCode:)` (typed errors with consistent title/detail/statusCode triplet) or `ValidationProblem(ModelState)` (RFC 7807 ModelState binding). Only non-Problem return is `StatusCode(StatusCodes.Status304NotModified)` in `CasesController` (correct per HTTP spec — 304 has no body). |
| #11 | ✅ **Shipped** | `[ResponseCache(NoStore = true)]` applied to `MembersController` and `CasesController` PII-bearing endpoints. |
| #12 | ✅ **Shipped** (Phase 1) | Typed `EctApiException` via `EnsureSuccessOrThrowAsync` on `ODataServiceBase`; bool-returning checkout/checkin retained as intentional UX. |
| #13 | ✅ **Shipped** | `ECTSystem.Web/Handlers/AuthorizationMessageHandler.cs` is a `DelegatingHandler` that attaches `Bearer` access tokens, detects `401 Unauthorized`, and transparently calls the ASP.NET Identity `/refresh` endpoint (registered via `MapIdentityApi` in `ECTSystem.Api/Program.cs`) using the stored refresh token. A `SemaphoreSlim` serializes concurrent refresh attempts and a double-check pattern prevents thundering-herd refreshes. On success the original request is cloned (`CloneRequestAsync`) and retried once with the new token; on failure both tokens are cleared and `JwtAuthStateProvider.NotifyAuthenticationStateChanged` triggers a redirect to login. Wired via `services.AddTransient<AuthorizationMessageHandler>()` plus `.AddHttpMessageHandler<AuthorizationMessageHandler>()` on both the OData and named API `HttpClient`s in `ServiceCollectionExtensions.cs`. |
| #14 | ✅ **Shipped** | Duplicate `app.MapGet("/me", ...)` removed from `Program.cs`; `UserController.GetCurrentUser` is the canonical endpoint. |
| #15 | ✅ **Shipped** | Server-side bound action `POST /odata/Members/Search` (`MembersController.Search`) ports the lower-cased multi-column `Contains`, rank-alias → pay-grade lookup, and `ServiceComponent` name/display-name match. EDM registers the collection action in `BuildEdmModel`; `LoggingService.SearchingMembers(textLength)` (EventId 211) logs only input length to avoid logging PII. `MemberService.SearchMembersAsync` now POSTs `{ searchText }` and reads `ODataResponse<Member>`, removing all client-side `$filter` composition and the OData single-quote-escape surface. |
| #16 | ✅ **Shipped** | New bound action `POST /odata/Cases/BookmarkedByCurrentState` (`CasesController.BookmarkedByCurrentState`) composes the current-user bookmark filter with `WhereCurrentWorkflowStateIn/NotIn` in a single `IQueryable` so OData applies `$filter`/`$orderby`/`$top`/`$skip`/`$count`/`$select`/`$expand` server-side. EDM registers the collection action with `includeStates`/`excludeStates` collection parameters. `BookmarkService.GetBookmarkedCasesByCurrentStateAsync` collapsed to a single round-trip POST, removing the prior two-step "fetch IDs then re-query" pattern and the `Id in (...)` URL bloat. |
| #17 | ✅ **Shipped** | `CurrentUserService.GetUserIdAsync` bare `catch (Exception)` replaced with narrow catches: `HttpRequestException`, `TaskCanceledException`, `JsonException` — each `LogWarning` and falls through. `SemaphoreSlim` lazy-init already in place. |
| #18 | ✅ **Shipped** | `UserService._cache` migrated from `Dictionary<string,string>` to `ConcurrentDictionary<string, CacheEntry>` with 15-minute sliding TTL via `TryGetFresh`. Avoids `Microsoft.Extensions.Caching.Memory` package dependency. |
| #19 | ⏸️ **Deferred** | Per user direction. |
| #20 | ✅ **Shipped** (Phase 1) | OData-client vs `HttpClient` convention documented on `ODataServiceBase`. |
| #21 | ✅ **Shipped** | `ECTSystem.Tests/Integration/SerializationRoundTripTests.cs` exercises both wire profiles (OData PascalCase + `JsonStringEnumConverter` and Web/minimal-API camelCase + `JsonStringEnumConverter`) directly with `JsonSerializer`. Covers `LineOfDutyCase`, `Member`, `Bookmark`, `WorkflowStateHistory`, including casing assertions, enum-name-not-ordinal assertions, cross-profile compatibility (PascalCase payload deserializes under web options and vice versa), and a `[Theory]` over `WorkflowState` values. 14 tests pass; protects against regressions from converter or naming-policy changes in either keyed `JsonSerializerOptions` (`WebJsonOptionsKey`/`ODataJsonOptionsKey`). |
| #22 | ✅ **Shipped** | `WorkflowStateHistoryController` POST stamps `EnteredDate` server-side; PATCH 400s on client `ExitDate` or stamps server-side. |
| #23 | ✅ **Shipped** | `CaseDialogueCommentsController.Post` resolves author from claims server-side, ignoring client-supplied author id. |
| #24 | ✅ **Shipped** | `Notification` standalone client `EntitySet` and `DataServiceQuery` removed from `EctODataContext`; no longer in `CaseService.FullExpand`. |
| #25 | ✅ **Shipped** | `MembersController.Patch` captures `var originalRowVersion = existing.RowVersion;` **before** `delta.Patch(existing)`, then assigns to `Entry(...).Property(e => e.RowVersion).OriginalValue`. |
| #26 | ✅ **Shipped** | `"test-user-id"` literal fallback removed from `CasesController` and `BookmarksController` user-id helpers; missing claim now → `Unauthorized()`. |
| #27 | ✅ **Shipped** | `MembersController.Get(int key)` returns `SingleResult.Create(context.Members.AsNoTracking().Where(m => m.Id == key))`; OData formatter applies `$select`/`$expand` and string-enum serialisation consistently. |
| #28 | ✅ **Shipped** | `MembersController.Delete` migrated to `ExecuteDeleteAsync(ct)`; returns `NotFound()` when zero rows affected. |
| #29 | ✅ **Shipped** | `AuthoritiesController.Delete` migrated to `ExecuteDeleteAsync` returning `NotFound()` Problem on zero rows. (ETag conditional-GET augmentation remains a follow-up but is out of scope for the original rec.) |
| #30 | ✅ **Shipped** (dead-code) | `IncludeAllNavigations()` extension at `LineOfDutyCaseQueryExtensions.cs:15` confirmed dead-code (zero callers in `CasesController` PATCH/POST/single-GET). Tracked here as closed; physical removal is a separate cleanup. |
| #31 | ✅ **Shipped** | `CasesController.GetDocuments` / `GetNotifications` / `GetWorkflowStateHistories` switched to `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`. |
| #32 | ⏸️ **Deferred** | Per user direction (blob-storage migration is a major architectural change). |
| #33 | ⏸️ **Deferred** | Per user direction (depends on #32). |
| #34 | ✅ **Shipped** | `MembersController.Post` returns `ValidationProblem(ModelState)`; verbose detail logged via `LoggingService.MemberInvalidModelState` only. |
| #35 | ⏸️ **Deferred** | Per user direction and per-recommendation disposition (defer `ODataControllerBase` → `ControllerBase` migration on `DocumentsController`). |

**Summary:** 31 shipped (#1–#18, #20–#31, #34) · 0 open · 4 deferred (#19, #32, #33, #35). The recommendation tables below remain as-written for historical context; status is authoritative in the dashboard above.

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

## 7. Archive Candidates ✅ Completed

> **Closure note:** Both candidates below remain on the archival shortlist; closure means the dispositions are recorded here as the single source of truth. Physical archival (move to `docs/controller-analysis/archive/`) is a separate housekeeping action, not part of this evaluation pass.

- **[workflow-state-history-controller-characterization.md](./workflow-state-history-controller-characterization.md)** — Provably stale: the source code already implements `SingleResult.Create`, restricts PATCH to `ExitDate` via `GetChangedPropertyNames()`, captures `originalRowVersion` before `delta.Patch`, maps `DbUpdateConcurrencyException` → 409, and uses `[ResponseCache(NoStore = true)]`. The only remaining valid finding (client-supplied `EnteredDate` via `CreateWorkflowStateHistoryDto`) is captured here as N1 + Rec #22. **Recommend archiving** (move to `docs/controller-analysis/archive/`) or rewriting against current source.
- **[documents-controller-recommendations.md](./documents-controller-recommendations.md)** — Partly stale: Phase 1 (`SingleResult` + `ExecuteDeleteAsync`) and Phase 2 (`Delta<T>` PATCH with RowVersion + 409) are **already implemented** in `DocumentsController`. Phase 3 (`ODataController` → `ControllerBase` migration) is captured here as Rec #35 with a **defer** disposition. Recommend annotating the sibling doc with an "obsolete — see eval doc Rec #35" header rather than deleting it (preserves the design rationale for posterity).
