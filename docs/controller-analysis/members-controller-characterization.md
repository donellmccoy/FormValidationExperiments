# MembersController — Capability Characterization

## Capabilities Inventory

| Capability | Endpoint | Pattern |
|---|---|---|
| Collection query | `GET /odata/Members` | Deferred `IQueryable` + `[EnableQuery]` |
| Single entity | `GET /odata/Members({key})` | Materialized `FirstOrDefaultAsync` — **not** `SingleResult<T>` |
| Create | `POST /odata/Members` | `[FromBody]` deserialized via System.Text.Json |
| Full replace | `PUT /odata/Members({key})` | `SetValues` with optimistic concurrency |
| Partial update | `PATCH /odata/Members({key})` | `Delta<T>` with optimistic concurrency |
| Delete | `DELETE /odata/Members({key})` | Two-roundtrip `FindAsync` + `Remove` + `SaveChangesAsync` |
| Navigation — Cases | `GET /odata/Members({key})/LineOfDutyCases` | Deferred `IQueryable` with filter |

---

## Strengths

### 1. Security — Authorization Enabled

`[Authorize]` is applied at class level and is **not** commented out—unlike CasesController and UserController. All endpoints require authentication.

### 2. OData Query Constraints

Consistent `[EnableQuery]` limits across all endpoints:
```csharp
[EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
```
`MaxNodeCount = 200` prevents query-of-death attacks. Collection endpoint adds `MaxTop = 100` and `PageSize = 50` for automatic server-driven paging.

### 3. Cache Control

Both `GET` endpoints use `[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]`. This is **correct** for mutable entity data—prevents stale reads.

### 4. Optimistic Concurrency on Both Write Paths

Both `PUT` and `PATCH` implement RowVersion-based optimistic concurrency:
```csharp
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = member.RowVersion;
```
Returns `409 Conflict` on `DbUpdateConcurrencyException`.

### 5. Structured Logging

All code paths invoke `LoggingService` methods (`QueryingMembers`, `RetrievingMember`, `MemberCreated`, etc.) with entity IDs. Model-state failures log the specific errors:
```csharp
LoggingService.MemberInvalidModelState($"Post — {string.Join("; ", errors)}");
```

---

## Weaknesses

### 1. `Get(key)` Materialises Instead of Returning `SingleResult<T>` — **Medium Risk**

```csharp
var member = await context.Members
    .AsNoTracking()
    .FirstOrDefaultAsync(m => m.Id == key, ct);
```

This executes the query before OData can append `$select`/`$expand`, so the server returns all columns even when the client asks for a subset. It also forces a manual `NotFound()` check that `SingleResult` handles automatically.

**Recommended fix:**
```csharp
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    LoggingService.RetrievingMember(key);
    var context = await CreateContextAsync(ct);
    return Ok(SingleResult.Create(
        context.Members.AsNoTracking().Where(m => m.Id == key)));
}
```

### 2. Both PUT and PATCH on the Same Entity — **Medium Risk (Serialization)**

MembersController is the only OData controller (besides DocumentsController) that exposes **both** `PUT` and `PATCH`. This creates three distinct serialization pathways for the same entity type:

| Method | Formatter | Enum Format |
|---|---|---|
| POST `[FromBody]` | System.Text.Json | Integer (default) |
| PUT `[FromBody]` | System.Text.Json | Integer (default) |
| PATCH `Delta<T>` | OData input formatter | String (OData convention) |

A client must know which serializer is active per HTTP method and vary enum formats accordingly. This is a maintenance hazard.

**Recommended approach:** Drop one of PUT/PATCH. If partial updates are needed (common), keep PATCH only and remove PUT. If full replacement semantics are needed, keep PUT only with strongly-typed DTOs.

### 3. PATCH Concurrency — RowVersion Read After `Delta.Patch` — **Medium Risk**

```csharp
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = existing.RowVersion;
```

After `delta.Patch(existing)` writes the incoming RowVersion into `existing.RowVersion`, the next line sets `OriginalValue` to the **already-overwritten** value. This means the concurrency check compares the client-supplied RowVersion against itself—it will *always* pass.

**Recommended fix — capture RowVersion before patching:**
```csharp
var originalRowVersion = existing.RowVersion;
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;
```

### 4. `Delta<T>` With Enum Property — **Medium Risk (Serialization)**

`Member` has one enum property:
```csharp
public ServiceComponent Component { get; set; }  // enum
```

When the OData input formatter deserialises `Delta<Member>`, `Component` must be sent as a string (`"RegAF"`, `"ANG"`, etc.). If the client sends an integer (matching `[FromBody]` conventions for POST and PUT), OData silently drops the property or throws.

**Recommended fix (long-term):** Follow the cross-cutting recommendation—keep OData for reads only, handle all writes through a normal EF Core `DbContext` with strongly-typed DTOs. This removes `Delta<T>` entirely.

### 5. Delete — Two Round Trips, No Concurrency Guard — **Low Risk**

```csharp
var member = await context.Members.FindAsync([key], ct);
// ...
context.Members.Remove(member);
await context.SaveChangesAsync(ct);
```

Two separate database calls. No RowVersion check — a stale delete succeeds silently. Compare with CasesController, which validates RowVersion on delete.

**Recommended fix — single-roundtrip:**
```csharp
var deleted = await context.Members.Where(m => m.Id == key).ExecuteDeleteAsync(ct);
if (deleted == 0)
    return NotFound();
```

Or, if concurrency matters, fetch and check RowVersion:
```csharp
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = rowVersion; // from If-Match header
context.Members.Remove(existing);
```

### 6. POST Detailed Error Logging May Leak Internals — **Low Risk**

```csharp
var errors = ModelState
    .Where(ms => ms.Value?.Errors.Count > 0)
    .Select(ms => $"{ms.Key}: [{string.Join(", ", ms.Value!.Errors.Select(e => e.ErrorMessage + (e.Exception != null ? $" ({e.Exception.Message})" : "")))}]");
```

Exception messages are logged, which is fine. But the same `ModelState` is returned via `BadRequest(ModelState)` to the client, which may include internal exception messages in development mode.

**Recommended fix:** Return `BadRequest(ModelState)` only with sanitised messages; log the detailed version internally.

### 7. Navigation Property `GetLineOfDutyCases` Caches Mutable Data — **Low Risk**

```csharp
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
```

A 60-second client cache on a collection that changes during active workflow processing. A new LOD case created for this member won't appear for up to a minute.

### 8. No `ProblemDetails` Error Responses — **Low Risk**

All error responses use bare `NotFound()`, `BadRequest()`, or `Conflict()`. RFC 9457 `ProblemDetails` would provide machine-parseable error bodies.

---

## Serialization Analysis

| Surface | Format In | Format Out | Enum Handling | Risk |
|---|---|---|---|---|
| `GET` collection | — | OData JSON (string enums) | `ServiceComponent` as string | **Low** |
| `GET(key)` | — | System.Text.Json (materialized) | `ServiceComponent` as int | **Medium** — different from collection GET |
| `POST [FromBody]` | System.Text.Json | OData JSON | `ServiceComponent` as int in → string out | **Medium** |
| `PUT [FromBody]` | System.Text.Json | OData JSON | `ServiceComponent` as int in → string out | **Medium** |
| `PATCH Delta<T>` | OData input formatter | OData JSON | `ServiceComponent` as string both ways | **Medium** — differs from POST/PUT input |
| Navigation GET | — | OData JSON (deferred) | Case enums as string | **Low** |

**Key concern:** `GET(key)` materialises the entity, so it bypasses the OData output formatter and uses System.Text.Json serialization. The same entity served from a collection `GET` goes through OData's JSON serializer instead. This means the **same client endpoint** gets different enum formats depending on whether it queries a collection or fetches by key.

---

## Enterprise-Grade Comparison

| Criterion | Current State | Enterprise Target |
|---|---|---|
| Single-entity query | `FirstOrDefaultAsync` — kills `$select`/`$expand` | `SingleResult<T>` deferred IQueryable |
| Write serialization | Three formatters (POST STJ, PUT STJ, PATCH OData) | Single DTO pathway with explicit mapping |
| PATCH concurrency | RowVersion overwritten before check | Capture RowVersion before `delta.Patch()` |
| Delete concurrency | None | RowVersion or `ExecuteDeleteAsync` |
| Error responses | Bare status codes | RFC 9457 ProblemDetails |
| Authorization | ✅ `[Authorize]` enabled | Role/policy-based per endpoint |
| Cache control | ✅ `NoStore` on reads; 60s on navigation | NoStore on all mutable endpoints |

---

## Bottom Line

MembersController is well-structured with proper authorization, concurrency control on updates, and comprehensive logging. The **PATCH RowVersion bug** (weakness #3) is the highest-priority fix—it silently disables optimistic concurrency. The `GET(key)` materialization and triple-serializer surface (POST + PUT + PATCH using different formatters) add unnecessary complexity and inconsistent enum handling. Consolidating writes to a single pathway (strongly-typed DTOs via EF Core) would eliminate the serialization split.
