# WorkflowStateHistoryController — Capability Characterization

## Capabilities Inventory

| Capability | Endpoint | Pattern |
|---|---|---|
| Collection query | `GET /odata/WorkflowStateHistory` | Deferred `IQueryable` + `[EnableQuery]` |
| Single entity | `GET /odata/WorkflowStateHistory({key})` | Materialized `FirstOrDefaultAsync` — **not** `SingleResult<T>` |
| Create | `POST /odata/WorkflowStateHistory` | `[FromBody]` deserialized via System.Text.Json |
| Partial update | `PATCH /odata/WorkflowStateHistory({key})` | `Delta<T>` — **no concurrency control** |
| Delete | *Not implemented* | By design — audit trail entries are immutable |

---

## Strengths

### 1. Security — Authorization Enabled

`[Authorize]` is applied at class level and is not commented out. All endpoints require authentication.

### 2. Append-Mostly Design

No `DELETE` endpoint. Workflow state history entries form an immutable audit trail—once created, they are never removed. `PATCH` exists only to set `ExitDate` when completing a workflow step, preserving the original creation record.

### 3. Correct Data Access on Collection GET

```csharp
var context = await CreateContextAsync(ct);
return Ok(context.WorkflowStateHistories.AsNoTracking());
```

Uses `CreateContextAsync` (registers context for disposal after OData serialization) and returns a deferred `IQueryable` for full OData query composition.

### 4. Consistent OData Query Constraints

```csharp
[EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
```

Matches other controllers' limits for `MaxTop`, `PageSize`, `MaxExpansionDepth`, and `MaxNodeCount`.

### 5. `Delta` Null Guard

```csharp
if (delta is null || !ModelState.IsValid)
```

Explicitly checks for a null `Delta<T>` — defensive against malformed requests where the OData input formatter produces null instead of an empty delta.

---

## Weaknesses

### 1. PATCH Has No Concurrency Control — **Critical**

```csharp
delta.Patch(existing);
await context.SaveChangesAsync(ct);
```

There is no `RowVersion` check. Two concurrent `PATCH` requests to the same entry will silently overwrite each other. In the workflow context, this means:
- Two clients could set different `ExitDate` values on the same step.
- The last write wins with no conflict detection.

Every other write-capable OData controller in this codebase implements RowVersion-based concurrency. This is the only `PATCH` endpoint that omits it.

**Recommended fix:**
```csharp
var originalRowVersion = existing.RowVersion;
delta.Patch(existing);
context.Entry(existing).Property(e => e.RowVersion).OriginalValue = originalRowVersion;

try
{
    await context.SaveChangesAsync(ct);
}
catch (DbUpdateConcurrencyException)
{
    return Conflict();
}
```

> **Prerequisite:** `WorkflowStateHistory` must have a `RowVersion` property. If the entity currently lacks one, add it:
> ```csharp
> [Timestamp]
> public byte[] RowVersion { get; set; }
> ```

### 2. `Get(key)` Materialises Instead of Returning `SingleResult<T>` — **Medium Risk**

```csharp
var entry = await context.WorkflowStateHistories
    .AsNoTracking()
    .FirstOrDefaultAsync(h => h.Id == key, ct);
```

Executes the query immediately. OData `$select`/`$expand` cannot be applied server-side. Returns all columns regardless of client request.

**Recommended fix:**
```csharp
[EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
{
    var context = await CreateContextAsync(ct);
    return Ok(SingleResult.Create(
        context.WorkflowStateHistories.AsNoTracking().Where(h => h.Id == key)));
}
```

### 3. `Duration = 60` Cache on Mutable Data — **Medium Risk**

Both `GET` endpoints use:
```csharp
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
```

Workflow state history entries are appended during active case processing. A 60-second client cache means:
- A newly created workflow step won't appear in the sidebar for up to a minute.
- A `PATCH`-updated `ExitDate` won't be reflected for up to a minute.
- The workflow timeline can show stale progress during active state transitions.

**Recommended fix:**
```csharp
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
```

This matches the pattern used by MembersController and CasesController's primary `GET` endpoints.

### 4. `Delta<T>` With Enum Property — **Medium Risk (Serialization)**

`WorkflowStateHistory` has one enum property:
```csharp
public WorkflowState WorkflowState { get; set; }  // enum
```

When the OData input formatter deserialises `Delta<WorkflowStateHistory>`, `WorkflowState` must be sent as a string (`"Draft"`, `"MemberReports"`, etc.). The `POST` endpoint uses `[FromBody]` (System.Text.Json), which expects an integer by default. A client must vary its enum serialization format between `POST` (integer) and `PATCH` (string) for the same entity type.

**Practical impact:** In normal usage, `WorkflowState` is set at creation time (POST) and never changed via PATCH—only `ExitDate` is patched. But the `Delta<T>` PATCH allows writing to *any* property, including `WorkflowState`, creating an unguarded mutation path for an audit field.

**Recommended fix (short-term):** Validate that `delta.GetChangedPropertyNames()` contains only `ExitDate`:
```csharp
var allowed = new HashSet<string> { "ExitDate" };
var changed = delta.GetChangedPropertyNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
if (!changed.IsSubsetOf(allowed))
    return BadRequest("Only ExitDate can be updated on workflow state history entries.");
```

**Recommended fix (long-term):** Replace `Delta<T>` PATCH with a dedicated endpoint:
```csharp
[HttpPost("{key}/complete")]
public async Task<IActionResult> Complete([FromODataUri] int key, CancellationToken ct = default)
{
    // Set ExitDate = DateTime.UtcNow — no Delta<T>, no serialization ambiguity
}
```

### 5. No Logging on Read Paths — **Low Risk**

Neither `GET` endpoint logs which entries are being queried. Compare with MembersController and CasesController, which log every read operation with entity IDs.

### 6. No Parent Entity Validation on POST — **Low Risk**

```csharp
context.WorkflowStateHistories.Add(entry);
await context.SaveChangesAsync(ct);
```

No check that `entry.LineOfDutyCaseId` references an existing case. The foreign key constraint will catch this at the database level, but the error response will be an unformatted `500` rather than a meaningful `400 Bad Request` or `404 Not Found`.

### 7. No `ProblemDetails` Error Responses — **Low Risk**

All error responses use bare `NotFound()` and `BadRequest(ModelState)`. RFC 9457 `ProblemDetails` would provide machine-parseable error bodies.

---

## Serialization Analysis

| Surface | Format In | Format Out | Enum Handling | Risk |
|---|---|---|---|---|
| `GET` collection | — | OData JSON (deferred) | `WorkflowState` as string | **Low** |
| `GET(key)` | — | System.Text.Json (materialized) | `WorkflowState` as int | **Medium** — differs from collection GET |
| `POST [FromBody]` | System.Text.Json | OData JSON | `WorkflowState` as int in → string out | **Medium** |
| `PATCH Delta<T>` | OData input formatter | OData JSON | `WorkflowState` as string both ways | **Medium** — differs from POST input |

**Key concern:** Same dual-serializer pattern as MembersController. The `GET(key)` materialization means a single entity fetched by key uses System.Text.Json (integer enums) while the same entity in a collection response uses OData JSON (string enums). Combined with the POST/PATCH input format split, clients must handle three different enum representations for the same `WorkflowState` property.

---

## Enterprise-Grade Comparison

| Criterion | Current State | Enterprise Target |
|---|---|---|
| PATCH concurrency | **None** — last write wins | RowVersion-based optimistic concurrency |
| Single-entity query | `FirstOrDefaultAsync` — kills `$select`/`$expand` | `SingleResult<T>` deferred IQueryable |
| Cache policy | `Duration = 60` on mutable workflow data | `NoStore` on all mutable endpoints |
| PATCH scope | Allows writing to any property | Restrict to `ExitDate` only |
| Write serialization | Dual formatter (POST STJ, PATCH OData) | Single DTO pathway or domain-specific action |
| Read logging | None | Log with case ID and entry ID |
| Error responses | Bare status codes | RFC 9457 ProblemDetails |
| Authorization | ✅ `[Authorize]` enabled | Role/policy-based per endpoint |
| Audit integrity | No DELETE (correct) | No DELETE + restricted PATCH scope |

---

## Bottom Line

WorkflowStateHistoryController has a sound append-mostly design (no DELETE, immutable audit trail). The **missing concurrency control on PATCH** is the highest-priority fix—it's the only write-capable OData controller in the codebase without optimistic concurrency. The unrestricted `Delta<T>` PATCH allows mutation of `WorkflowState` itself (an audit field that should be immutable after creation), and the 60-second cache on actively mutating workflow data will cause stale sidebar displays. The dual-serializer issue is present but lower risk here since `WorkflowState` is typically set only at POST time.
