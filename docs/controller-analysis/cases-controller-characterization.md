# CasesController — Capability Characterization

## Capabilities Inventory

| Capability | Endpoint | Pattern |
|---|---|---|
| Collection query | `GET /odata/Cases` | Deferred `IQueryable` + `[EnableQuery]` |
| Single entity | `GET /odata/Cases({key})` | Deferred `IQueryable` + `SingleResult.Create()` with ETag/304 |
| Create | `POST /odata/Cases` | `[FromBody]` + CaseId generation + retry loop |
| Partial update | `PATCH /odata/Cases({key})` | `Delta<T>` with optimistic concurrency |
| Checkout | `POST /odata/Cases({key})/Checkout` | Custom `[HttpPost]` action |
| Checkin | `POST /odata/Cases({key})/Checkin` | Custom `[HttpPost]` action |
| Delete | `DELETE /odata/Cases({key})` | `IncludeAllNavigations` + cascade + concurrency |
| Bookmarked cases | `GET /odata/Cases/Default.Bookmarked()` | OData bound function (user-filtered) |
| Filter by state | `GET /odata/Cases/ByCurrentState(...)` | OData bound function (subquery-based) |
| Nav: Documents | `GET /odata/Cases({key})/Documents` | Collection nav, deferred `IQueryable` |
| Nav: Notifications | `GET /odata/Cases({key})/Notifications` | Collection nav, deferred `IQueryable` |
| Nav: WorkflowStateHistories | `GET /odata/Cases({key})/WorkflowStateHistories` | Collection nav, deferred `IQueryable` |
| Nav: Member | `GET /odata/Cases({key})/Member` | Single-valued nav, `SingleResult` |
| Nav: MEDCON | `GET /odata/Cases({key})/MEDCON` | Single-valued nav, `SingleResult` |
| Nav: INCAP | `GET /odata/Cases({key})/INCAP` | Single-valued nav, `SingleResult` |

---

## Strengths

### 1. ETag / Conditional GET Support

`Get(key)` implements proper conditional GET with `If-None-Match` / `304 Not Modified`. The ETag is derived from the `RowVersion` column via a lightweight single-column query — no full entity load needed for cache validation:

```csharp
var rowVersion = await context.Cases
    .Where(c => c.Id == key)
    .Select(c => c.RowVersion)
    .FirstOrDefaultAsync(ct);

var etag = $"\"{Convert.ToBase64String(rowVersion)}\"";

if (Request.Headers.IfNoneMatch.ToString() == etag)
    return StatusCode(StatusCodes.Status304NotModified);

Response.Headers.ETag = etag;
Response.Headers.CacheControl = "private, max-age=0, must-revalidate";
```

This is the **only controller** in the codebase implementing ETag-based caching — a genuine enterprise-grade pattern.

### 2. CaseId Generation with Serialized Concurrency

CaseId generation (`YYYYMMDD-XXX` format) uses raw SQL with `UPDLOCK, HOLDLOCK` hints to serialize concurrent readers on the same date prefix. A retry loop handles the rare case where the unique index is violated despite the lock:

```csharp
var maxSuffix = await context.Database
    .SqlQueryRaw<string>(
        """
        SELECT MAX(SUBSTRING(CaseId, LEN(@p0) + 1, ...)) AS [Value]
        FROM Cases WITH (UPDLOCK, HOLDLOCK)
        WHERE CaseId LIKE @p0 + '%'
        """, prefix)
    .FirstOrDefaultAsync(ct);
```

The retry loop properly detaches the failed entity and clears the `WorkflowStateHistories` collection before re-generating the CaseId. Maximum 3 retries.

### 3. SingleResult on `Get(key)` — Correct OData Composition

Unlike `AuthoritiesController`, `MembersController`, and `WorkflowStateHistoryController` which eagerly materialize the entity on `Get(key)`, CasesController correctly returns a deferred `IQueryable` via `SingleResult.Create()`. This enables full OData `$select`/`$expand` composition server-side.

### 4. Comprehensive Navigation Endpoints

Six navigation-property endpoints cover all collection and single-valued navigations. All collection navs return deferred `IQueryable` for OData composition. Single-valued navs (Member, MEDCON, INCAP) correctly use `SingleResult.Create()` with a projection:

```csharp
return SingleResult.Create(context.Cases.AsNoTracking().Where(c => c.Id == key).Select(c => c.Member));
```

### 5. OData Bound Functions

Two bound functions expose server-side filtering logic that cannot be expressed in standard `$filter`:

- **`Bookmarked()`** — returns cases bookmarked by the current user via a cross-table subquery.
- **`ByCurrentState(includeStates, excludeStates)`** — filters by the most recent `WorkflowStateHistory` entry, composable with additional OData query options.

Both return deferred `IQueryable` for full OData composition.

### 6. Checkout/Checkin Guard on Delete

Delete prevents deleting a case that is currently checked out:

```csharp
if (lodCase.IsCheckedOut)
{
    LoggingService.CaseCheckedOutByAnother(key, lodCase.CheckedOutByName);
    return Conflict();
}
```

### 7. Structured Logging Throughout

Nearly every operation has dedicated `LoggingService` calls: `QueryingCases`, `RetrievingCase`, `CaseNotFound`, `CaseCreated`, `PatchingCase`, `CasePatched`, `CheckingOutCase`, `CaseCheckedOut`, `DeletingCase`, `CaseDeleted`, `QueryingCaseNavigation`. The richest logging of any controller.

### 8. Data Access Patterns

- `CreateContextAsync()` (registered for disposal) on all deferred `IQueryable` returns.
- `await using var context` on all mutation paths (POST, PATCH, Checkout, Checkin, Delete).
- `AsNoTracking()` on all read paths.
- `AsSplitQuery()` on `Get(key)` to prevent Cartesian products when OData `$expand` is applied.
- `IncludeWorkflowState()` on PATCH/POST reload to include only what the client needs.
- `IncludeAllNavigations()` on Delete to load cascade-dependent entities.

---

## Weaknesses

### 1. `[Authorize]` Is Commented Out — **CRITICAL Security Gap**

The `[Authorize]` attribute is commented out. Any anonymous user can perform all CRUD operations, checkout/checkin cases, and access all case data. This is the most complex and sensitive controller in the application.

```csharp
//[Authorize]
public class CasesController : ODataControllerBase
```

**Recommended Fix:** Uncomment and add role-based constraints:

```csharp
[Authorize]
public class CasesController : ODataControllerBase
{
    // ... existing code ...

    [Authorize(Roles = "Admin,CaseManager")]
    public async Task<IActionResult> Delete(...)
}
```

### 2. `Console.WriteLine` Debug Code in Production

`GetUserId()` contains a `Console.WriteLine` debug statement that logs the user ID claim to stdout in production:

```csharp
private string GetUserId() {
    var id = User?.FindFirstValue(ClaimTypes.NameIdentifier);
    Console.WriteLine("USER ID CLAIM IS: " + (id ?? "NULL"));
    //                 ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
    //                 Debug output in production
    return id ?? "test-user-id";
}
```

**Recommended Fix:** Remove `Console.WriteLine` and use structured logging. Also throw when the claim is absent (see Weakness #3):

```csharp
private string GetUserId()
{
    return User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("Missing NameIdentifier claim.");
}
```

### 3. Hardcoded Fallback `"test-user-id"` — **Security Vulnerability**

Same issue as `BookmarksController`. When the claim is missing, all user-scoped operations (Checkout, Checkin, Bookmarked function) execute against `"test-user-id"`. Combined with the commented-out `[Authorize]`, this means **any anonymous request** will appear to be from `"test-user-id"`.

**Impact:** Critical — cross-user data leakage, unauthorized case checkout, and bookmark pollution.

### 4. Serialization — `Delta<T>` on a Complex Entity with Many Enum Properties

`LineOfDutyCase` has **7+ enum properties**: `ProcessType`, `Component` (ServiceComponent), `IncidentType`, `IncidentDutyStatus` (DutyStatus), `SubstanceType?`, `FinalFinding` (FindingType), `BoardFinding?`, `ApprovingFinding?`. `Delta<T>` is populated by the **OData input formatter**, which:

- Receives enum values as **strings** (e.g., `"RegAF"`, `"Injury"`, `"InLineOfDuty"`).
- Creates CLR enum values internally, then `Patch()` copies them to the tracked entity.
- EF Core stores these as **integers** in the database.

**Risks:**

1. **Enum name mismatch:** If a client sends a casing variant (e.g., `"regaf"` or `"REGAF"`) that the OData deserializer doesn't handle, `Delta<T>` will silently leave the property unchanged or throw.
2. **Nullable enum handling:** `SubstanceType?`, `BoardFinding?`, `ApprovingFinding?` must correctly handle `null` values through the OData formatter → `Delta<T>` → EF Core chain. A serialization mismatch anywhere in this chain can produce a `NullReferenceException` or silently drop the property update.
3. **`Delta<T>` tracks changed properties** — if the OData formatter fails to parse an enum property, it won't appear in `Delta.GetChangedPropertyNames()`, and the update will silently skip it. The client will think the update succeeded, but the enum property won't change.

**Architectural Recommendation:** Replace `Delta<T>` with strongly-typed update DTOs:

```csharp
public async Task<IActionResult> Patch([FromODataUri] int key, [FromBody] UpdateCaseDto dto, CancellationToken ct)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var existing = await context.Cases.FindAsync([key], ct);
    if (existing is null) return NotFound();

    dto.ApplyTo(existing);  // Explicit, type-safe property mapping

    context.Entry(existing).Property(e => e.RowVersion).OriginalValue = dto.RowVersion;
    await context.SaveChangesAsync(ct);

    return Updated(existing);
}
```

### 5. Checkout/Checkin Lack Concurrency Control

Neither `Checkout` nor `Checkin` check `RowVersion`. Two concurrent checkout requests will both succeed — a classic TOCTOU (time-of-check-time-of-use) race:

1. Request A reads `IsCheckedOut == false`
2. Request B reads `IsCheckedOut == false`
3. Request A sets `IsCheckedOut = true`, saves
4. Request B sets `IsCheckedOut = true`, saves (overwrites A's checkout)

**Recommended Fix:** Add `RowVersion` to the checkout flow:

```csharp
[HttpPost]
public async Task<IActionResult> Checkout([FromODataUri] int key, [FromBody] CheckoutRequest request, CancellationToken ct)
{
    // ... FindAsync and IsCheckedOut check ...

    context.Entry(existing).Property(e => e.RowVersion).OriginalValue = request.RowVersion;

    try { await context.SaveChangesAsync(ct); }
    catch (DbUpdateConcurrencyException) { return Conflict(); }

    return Ok(existing);
}
```

### 6. POST `[FromBody]` Deserializes `LineOfDutyCase` Directly — Serialization Risk

POST uses `[FromBody]` to deserialize the full `LineOfDutyCase` entity via `System.Text.Json`. The same entity is also used with `Delta<T>` (OData formatter) on PATCH. This means:

- **POST:** Client must send enum values as **integers** (or strings if STJ is configured with `JsonStringEnumConverter`) — `System.Text.Json` formatting rules apply.
- **PATCH:** Client must send enum values as **strings** — OData formatting rules apply.

A client using the same model for both POST and PATCH must switch serialization behavior between operations. This is a maintenance trap.

**Recommended Fix:** Use a `CreateCaseDto` for POST:

```csharp
public async Task<IActionResult> Post([FromBody] CreateCaseDto dto, CancellationToken ct)
{
    var lodCase = dto.ToEntity();
    // ... CaseId generation and save ...
}
```

### 7. Delete Loads All Navigation Properties — Performance Overhead

Delete uses `IncludeAllNavigations()` which eagerly loads **10 navigation collections** via `AsSplitQuery()` (11 SQL queries) just to delete the case. This is necessary for EF Core's client-cascade delete behavior, but it's expensive.

**Recommended Fix:** Use a database-side cascade delete (configure `ON DELETE CASCADE` in the migration) or use `ExecuteDeleteAsync` on child tables:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync(ct);
await context.Documents.Where(d => d.LineOfDutyCaseId == key).ExecuteDeleteAsync(ct);
await context.Notifications.Where(n => n.LineOfDutyCaseId == key).ExecuteDeleteAsync(ct);
// ... other child tables ...
await context.Cases.Where(c => c.Id == key).ExecuteDeleteAsync(ct);
await transaction.CommitAsync(ct);
```

### 8. Manual MEDCON/INCAP Delete — Indicates Missing Cascade Configuration

Delete manually removes `MEDCON` and `INCAP` entities after loading them through `IncludeAllNavigations()`:

```csharp
if (lodCase.MEDCON is not null)
    context.MEDCONDetails.Remove(lodCase.MEDCON);

if (lodCase.INCAP is not null)
    context.INCAPDetails.Remove(lodCase.INCAP);
```

This suggests the EF Core model lacks proper cascade-delete configuration for these relationships. If the database has `ON DELETE CASCADE`, this code is redundant. If it doesn't, this code is fragile — any new single-valued navigation property added to `LineOfDutyCase` must also be manually removed here.

### 9. `MaxNodeCount` Inconsistency

Collection-level endpoints use `MaxNodeCount=500` while single-entity and navigation endpoints use `MaxNodeCount=200`. The higher limit on collection endpoints may allow complex query expressions that could be expensive to evaluate.

### 10. Navigation Endpoint Caching — Stale Data Risk

`GetDocuments`, `GetNotifications`, and `GetWorkflowStateHistories` use `ResponseCache(Duration=60)`. These are **mutable collections** — a document upload or workflow transition within the 60-second window will not be visible to the client.

**Recommended Fix:** Use `NoStore=true` or implement ETag-based conditional GET on navigation properties. Alternatively, reduce the cache duration to match the expected mutation frequency.

### 11. `Bookmarked()` Function — No Existence Validation

The `Bookmarked()` function doesn't validate that cases still exist or are in an accessible state. A deleted case with an orphaned bookmark entry would appear in results.

### 12. Error Responses Inconsistency

- PATCH and Delete use bare `Conflict()` for concurrency errors — no Problem Details.
- Checkout uses bare `Conflict()` for "already checked out" — no detail about who has it checked out (the `LoggingService` logs this, but the client doesn't receive it).
- POST returns bare `BadRequest(ModelState)` for validation errors.

**Recommended Fix:**

```csharp
// Checkout conflict with details
return Problem(
    title: "Case already checked out",
    detail: $"Case {key} is checked out by {existing.CheckedOutByName}.",
    statusCode: StatusCodes.Status409Conflict);

// Concurrency conflict
return Problem(
    title: "Concurrency conflict",
    detail: "The case was modified by another user. Refresh and retry.",
    statusCode: StatusCodes.Status409Conflict);
```

---

## Serialization Analysis

| Concern | Status | Impact |
|---|---|---|
| `Delta<T>` with 7+ enum properties | ⚠️ **High risk** | OData string-format enums → CLR values → EF Core int storage. Silent failures on parse errors — property update silently dropped. |
| Nullable enums in `Delta<T>` | ⚠️ **High risk** | `SubstanceType?`, `BoardFinding?`, `ApprovingFinding?` — null handling through OData → Delta → EF Core chain is fragile. |
| `[FromBody]` on POST vs. OData formatter on PATCH | ⚠️ **Inconsistent** | Same entity, two different serializers. Client must vary enum format per operation. |
| POST entity directly binds to `LineOfDutyCase` | ⚠️ Over-posting risk | Client can set `Id`, `RowVersion`, `CaseId`, and any other property. No DTO-level validation. |
| ETag from `RowVersion` on GET | ✅ Correct | Base64-encoded RowVersion as ETag string — proper format. |
| `SingleResult.Create()` on `Get(key)` | ✅ Correct | OData middleware serializes from deferred IQueryable. |
| Navigation endpoint `IQueryable` | ✅ Correct | Deferred queries — OData composes `$filter`/`$select` on child collections. |
| `SingleResult` on Member/MEDCON/INCAP | ✅ Correct | Projection via `.Select(c => c.MEDCON)` — serialized correctly. |
| Checkout/Checkin response serialization | ⚠️ Returns tracked entity | `Ok(existing)` returns the tracked entity directly — OData serializer accesses EF Core change-tracked entity. Works but risks lazy-loading if navigation properties are configured. |
| POST reload for response | ✅ Correct | Response re-queried with `AsNoTracking` + `IncludeWorkflowState` — clean serialization. |

---

## Enterprise-Grade Comparison

| Capability | This Controller | Enterprise Standard |
|---|---|---|
| Auth | **Commented out** | RBAC + policy-based authorization |
| Concurrency | ✅ RowVersion on PATCH/Delete | ✅ Missing on Checkout/Checkin |
| ETag caching | ✅ Conditional GET on single entity | ETag on all GET endpoints |
| Error format | Bare status codes | RFC 9457 Problem Details |
| Logging | ✅ Comprehensive | ✅ Comprehensive |
| ID generation | ✅ UPDLOCK serialization + retry | ✅ Robust |
| OData compliance | ✅ SingleResult, functions, navs | ✅ Well-implemented |
| Write path | `Delta<T>` + `[FromBody]` mixed | Strongly-typed DTOs throughout |
| Delete | Full entity graph load | Database cascade or targeted `ExecuteDeleteAsync` |
| Over-posting | No DTO on POST | `CreateCaseDto` with allowed fields |
| Input validation | `ModelState.IsValid` only | FluentValidation + domain rules |

---

## Bottom Line

`CasesController` is the most feature-rich controller in the codebase, with genuine enterprise patterns (ETag caching, `SingleResult`, bound functions, structured logging, retry logic). The critical concerns are:

1. **`[Authorize]` commented out** — any anonymous user has full CRUD access to the most sensitive entity.
2. **`Console.WriteLine` debug code** — information leakage in production.
3. **`Delta<T>` with 7+ enum properties** — the highest serialization risk in the codebase. Silent property update failures on enum parse errors.
4. **Dual serializer problem** — POST uses `System.Text.Json`, PATCH uses OData formatter. Same entity requires different client-side serialization per HTTP method.
5. **Checkout/Checkin TOCTOU race** — concurrent checkouts succeed without concurrency control.
6. **Over-posting on POST** — `[FromBody] LineOfDutyCase` allows the client to set any property including `Id`, `RowVersion`, and `CaseId`.
