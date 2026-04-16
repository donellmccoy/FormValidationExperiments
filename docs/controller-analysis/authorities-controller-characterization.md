# AuthoritiesController — Capability Characterization

## Capabilities Inventory

| Capability | Endpoint | Pattern |
|---|---|---|
| Collection query | `GET /odata/Authorities` | Deferred `IQueryable` + `[EnableQuery]` |
| Single entity | `GET /odata/Authorities({key})` | Eager `FirstOrDefaultAsync` (materialized) |
| Create | `POST /odata/Authorities` | `[FromBody]` + EF Core `Add` |
| Partial update | `PATCH /odata/Authorities({key})` | `Delta<T>` with optimistic concurrency |
| Delete | `DELETE /odata/Authorities({key})` | `FindAsync` + `Remove` + `SaveChangesAsync` |

---

## Strengths

### 1. Security — Class-Level Authorization

- `[Authorize]` at class level — no anonymous access to any endpoint.
- Follows the same pattern as `BookmarksController` and `MembersController`.

### 2. OData Compliance

- `[EnableQuery]` on all read and mutation endpoints — allows OData query composition on responses.
- Query limits (`MaxTop=100`, `PageSize=50`, `MaxExpansionDepth=3`, `MaxNodeCount=200`) prevent query abuse.
- `Delta<T>` on PATCH — true partial-update semantics.
- Convention routing via `ODataControllerBase`.

### 3. Concurrency Control

- Optimistic concurrency on PATCH via `RowVersion` byte[] — sets `OriginalValue` from client-provided value.
- Clean `DbUpdateConcurrencyException` → `409 Conflict` mapping.

### 4. Data Access

- `IDbContextFactory<EctDbContext>` via `ODataControllerBase` — proper scoped context lifetime.
- `AsNoTracking()` on read paths — eliminates change-tracker overhead.
- `CreateContextAsync()` on collection GET — registers context for disposal during OData serialization, keeping the `IQueryable` alive.

### 5. Caching

- `ResponseCache(Duration=60)` on both GET endpoints — enables client-side caching for relatively stable authority data.

---

## Weaknesses

### 1. `Get(key)` Materializes the Entity — Breaks OData Composition

The single-entity GET uses `FirstOrDefaultAsync`, which eagerly materializes the entity before `[EnableQuery]` can act. The `$select` and `$expand` OData options have no effect because the entity is already loaded.

**Recommended Fix:** Return a deferred `IQueryable` via `SingleResult.Create()` so OData middleware can apply `$select`/`$expand` server-side:

```csharp
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    var context = await CreateContextAsync(ct);
    var query = context.Authorities.AsNoTracking().Where(a => a.Id == key);
    return Ok(SingleResult.Create(query));
}
```

### 2. Serialization — `Delta<T>` / OData vs. EF Core Tracking Conflict

The PATCH endpoint uses OData's `Delta<T>.Patch(existing)` which applies changed properties to a tracked EF Core entity. This introduces a subtle coupling:

- `Delta<T>` is populated by the **OData input formatter** (not `System.Text.Json`). It tracks which properties the client sent.
- `Patch()` writes those properties onto the tracked entity, then EF Core's change tracker detects those as modifications.
- **Risk:** If `Delta<T>` includes navigation property keys or complex types that OData serializes differently from how EF Core expects them, the change tracker may misinterpret the state. Enum properties are particularly vulnerable — OData serializes enums as strings (`"RegAF"`) while EF Core stores them as integers.

**Architectural Recommendation:** Keep OData only for query/expand/metadata. Perform all writes through a normal EF Core `DbContext` with strongly-typed DTOs:

```csharp
public async Task<IActionResult> Patch([FromODataUri] int key, [FromBody] UpdateAuthorityDto dto, CancellationToken ct)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var existing = await context.Authorities.FindAsync([key], ct);
    if (existing is null) return NotFound();

    // Map only provided fields from DTO
    if (dto.Name is not null) existing.Name = dto.Name;
    if (dto.Title is not null) existing.Title = dto.Title;
    // ... map relevant fields

    context.Entry(existing).Property(e => e.RowVersion).OriginalValue = dto.RowVersion;
    await context.SaveChangesAsync(ct);
    return Updated(existing);
}
```

### 3. Delete Uses Two Round Trips

`Delete` uses `FindAsync` (SELECT) then `Remove` + `SaveChangesAsync` (DELETE) — two SQL round trips. `ExecuteDeleteAsync` would be a single-statement delete.

**Recommended Fix:**

```csharp
public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var deleted = await context.Authorities
        .Where(a => a.Id == key)
        .ExecuteDeleteAsync(ct);

    return deleted == 0 ? NotFound() : NoContent();
}
```

### 4. No Resource-Level Authorization

`[Authorize]` ensures the user is authenticated, but there's no role-based or resource-based access control. Any authenticated user can create, modify, or delete any authority record.

**Recommended Fix:** Add role-based restrictions — authority management should be limited to admin or designated roles:

```csharp
[Authorize(Roles = "Admin,CaseManager")]
public async Task<IActionResult> Post(...)

[Authorize(Roles = "Admin")]
public async Task<IActionResult> Delete(...)
```

### 5. No Structured Logging

Unlike `BookmarksController`, `CasesController`, and `MembersController` which call dedicated `LoggingService` methods, `AuthoritiesController` doesn't log any operations. There is no observability for authority mutations.

**Recommended Fix:** Add logging calls using the existing `ILoggingService` pattern:

```csharp
LoggingService.QueryingAuthorities();          // GET collection
LoggingService.RetrievingAuthority(key);       // GET single
LoggingService.AuthorityCreated(authority.Id);  // POST
LoggingService.PatchingAuthority(key);          // PATCH
LoggingService.DeletingAuthority(key);          // DELETE
```

### 6. Error Responses Lack Problem Details

Errors return bare `BadRequest(ModelState)`, `NotFound()`, or `Conflict()` without RFC 9457 Problem Details. This is inconsistent with the `DocumentsController` which has been upgraded to use `Problem()`.

**Recommended Fix:** Return typed `Problem()` responses for machine-parseable errors:

```csharp
catch (DbUpdateConcurrencyException)
{
    return Problem(
        title: "Concurrency conflict",
        detail: "The authority was modified by another user. Refresh and retry.",
        statusCode: StatusCodes.Status409Conflict);
}
```

### 7. POST Uses `[FromBody]` — Serialization Format Mismatch Risk

POST uses `[FromBody]` which routes through `System.Text.Json` input formatter. This works correctly when the client sends standard JSON. However, if the OData client sends an OData-formatted payload (with `@odata.type` annotations, string-serialized enums), `System.Text.Json` may fail to deserialize enum properties or ignore OData annotations.

**Impact:** Low for `LineOfDutyAuthority` which has few/no enum properties, but this pattern inconsistency (POST uses `[FromBody]`, PATCH uses OData Delta) means two different deserializers handle the same entity type. Any future enum or complex-type additions to `LineOfDutyAuthority` would need to work with both serializers.

---

## Serialization Analysis

| Concern | Status | Impact |
|---|---|---|
| `Delta<T>` enum serialization | ⚠️ Potential risk | OData serializes enums as strings; EF Core stores as int. `Delta.Patch()` copies the value directly — works if CLR enum value is correct, but breaks if OData sends the string name and the property setter expects an int. |
| `[FromBody]` on POST vs. OData formatter on PATCH | ⚠️ Inconsistent | Two different deserializers handle the same entity type. Changes to the model must be validated against both paths. |
| `SingleResult` not used on `Get(key)` | ⚠️ Limits composition | Response serialized from materialized entity — `$select` ignored, full entity always serialized. |
| Collection GET returns deferred `IQueryable` | ✅ Correct | OData middleware serializes directly from EF Core query — no intermediate materialization. |
| Navigation properties | N/A | `LineOfDutyAuthority` has no navigation properties exposed — no circular reference or lazy-loading serialization risks. |

---

## Enterprise-Grade Comparison

| Capability | This Controller | Enterprise Standard |
|---|---|---|
| Auth | `[Authorize]` (authn only) | RBAC + policy-based authorization |
| Concurrency | ✅ `RowVersion` optimistic locking | `ETag` / `If-Match` headers |
| Error format | Bare status codes | RFC 9457 Problem Details |
| Logging | None | Structured logging on all operations |
| Caching | ✅ `ResponseCache(Duration=60)` | `ETag`-based conditional GET |
| Delete | Two round trips | Single `ExecuteDeleteAsync` |
| OData compliance | Mostly correct | `SingleResult` on single-entity GET |
| Write path | `Delta<T>` (OData formatter) | Strongly-typed DTOs via EF Core |

---

## Bottom Line

`AuthoritiesController` is a clean, minimal CRUD controller with correct OData query limits and concurrency control. The key concerns are:

1. **Serialization inconsistency** — POST uses `System.Text.Json` (`[FromBody]`), PATCH uses OData's `Delta<T>` formatter. Adding enum properties to the model creates a dual-serializer maintenance burden.
2. **`Get(key)` breaks OData composition** — eagerly materializes instead of using `SingleResult.Create()`.
3. **No logging** — silent on all operations unlike sibling controllers.
4. **No resource-level authorization** — any authenticated user can mutate authority records.
