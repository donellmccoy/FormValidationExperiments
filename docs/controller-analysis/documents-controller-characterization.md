# DocumentsController — Capability Characterization

## Capabilities Inventory

| Capability | Endpoint | Pattern |
|---|---|---|
| Collection query | `GET /odata/Documents` | Deferred `IQueryable` + `[EnableQuery]` |
| Single entity | `GET /odata/Documents({key})` | `SingleResult<T>` deferred query |
| Partial update | `PATCH /odata/Documents({key})` | `Delta<T>` with optimistic concurrency |
| Full replace | `PUT /odata/Documents({key})` | `SetValues` with optimistic concurrency |
| Delete | `DELETE /odata/Documents({key})` | Single-roundtrip `ExecuteDeleteAsync` |
| Binary download | `GET /odata/Documents({key})/$value` | Projected select → `File()` |
| Multi-file upload | `POST /odata/Cases({caseId})/Documents` | `IFormFile` batch with validation |
| PDF generation | `GET /odata/Cases({caseId})/Form348` | Custom hybrid route |

---

## Strengths

### 1. Security — File Upload Hardening

- Extension allowlist (`AllowedExtensions`) prevents arbitrary file types
- Magic-byte signature validation (`FileSignatures`) defeats extension spoofing — this is above-average; many enterprise APIs only check the extension
- Per-file size cap (10 MB) + aggregate request limit (50 MB) via `[RequestSizeLimit]`
- `[Authorize]` at class level — no anonymous access
- Projected `Select` in `GetValue` avoids loading unnecessary columns

### 2. OData Compliance

- Proper `SingleResult<T>` on `Get(key)` — allows `$select`/`$expand` server-side composition on single-entity queries
- `Delta<T>` for PATCH — true partial-update semantics, not full-entity replacement
- Query limits (`MaxTop=100`, `PageSize=50`, `MaxExpansionDepth=3`, `MaxNodeCount=200`) prevent OData query abuse
- Convention routing via `ODataController` base

### 3. Concurrency Control

- Optimistic concurrency on both PATCH and PUT via `RowVersion` byte[] (`[Timestamp]` + `[ConcurrencyCheck]`)
- Clean `DbUpdateConcurrencyException` → `409 Conflict` mapping

### 4. Data Access

- `IDbContextFactory<EctDbContext>` — proper scoped context lifetime for concurrent requests (no shared DbContext)
- `AsNoTracking()` on all read paths — eliminates change-tracker overhead
- `ExecuteDeleteAsync` on Delete — single-roundtrip, no SELECT before DELETE
- `CreateContextAsync` registers context for disposal at response end — keeps `IQueryable` alive through serialization

### 5. Observability

- Structured logging via `ILoggingService` with dedicated methods per operation (not ad-hoc string interpolation)
- Logs on entry, success, not-found, and error branches

---

## Weaknesses

### 1. Binary Content in Database (Architecture)

The `Content` column is `varbinary(max)` stored in SQL Server. Enterprise APIs typically use Azure Blob Storage / S3 with a metadata-only database row + a pre-signed download URL. Storing binary in the DB causes:

- Table/index bloat and backup size inflation
- Full-row locks during large reads/writes
- Memory pressure on upload (`MemoryStream` → `ToArray()` buffers the entire file in RAM)
- No CDN/caching tier for downloads

**Recommended Fix:** Introduce an `IBlobStorageService` abstraction backed by Azure Blob Storage. Replace the `Content` column with a `BlobUri` string column. On upload, stream the file directly to blob storage via `BlobClient.UploadAsync(stream)` and store only the blob URI in the database. On download, generate a time-limited SAS URL and redirect the client (`302`) or proxy-stream via `BlobClient.OpenReadAsync()`. Add a data migration to move existing `varbinary(max)` rows to blob containers.

```csharp
public interface IBlobStorageService
{
    Task<Uri> UploadAsync(Stream content, string fileName, string contentType, CancellationToken ct);
    Task<Stream> OpenReadAsync(string blobUri, CancellationToken ct);
    Task DeleteAsync(string blobUri, CancellationToken ct);
    Uri GenerateSasUri(string blobUri, TimeSpan expiry);
}
```

### 2. Upload Streams Fully Buffered in Memory

Each file is `CopyToAsync` into a `MemoryStream`, then `ToArray()`. For a 50 MB batch, this allocates 50 MB on the Large Object Heap. Enterprise grade: stream directly to blob storage or use `IFormFile.CopyToAsync(targetStream)`.

**Recommended Fix:** If blob storage is adopted (see #1), stream directly from `IFormFile.OpenReadStream()` to `BlobClient.UploadAsync()` — zero intermediate buffer. If the database must remain the target short-term, use `RecyclableMemoryStream` from `Microsoft.IO.RecyclableMemoryStream` to avoid Large Object Heap fragmentation:

```csharp
var manager = new RecyclableMemoryStreamManager();
await using var ms = manager.GetStream();
await f.CopyToAsync(ms, ct);
document.Content = ms.ToArray();
```

### 3. No Transactional Consistency on Multi-File Upload ✅ IMPLEMENTED

The upload loop adds multiple entities then calls `SaveChangesAsync` once — good for atomicity. However, if the validation loop passes but `SaveChangesAsync` fails partway (e.g., FK violation on `caseId`), there's no explicit transaction, so the behavior depends on the provider's implicit transaction.

> **Status:** Implemented. Upload now wraps the entire operation in an explicit `BeginTransactionAsync`/`CommitAsync` with `RollbackAsync` on failure. FK validation (`Cases.AnyAsync`) is performed before entering the file-processing loop. Failures are logged via `LoggingService.UploadFailed()` and re-thrown.

**Recommended Fix:** Wrap the upload in an explicit `IDbContextTransaction`. Validate the FK (`caseId` exists) before entering the loop. If blob storage is used, track uploaded blob URIs and delete them in the `catch` block on rollback:

```csharp
await using var transaction = await context.Database.BeginTransactionAsync(ct);
try
{
    // Verify case exists
    if (!await context.Cases.AnyAsync(c => c.Id == caseId, ct))
        return NotFound($"Case {caseId} not found.");

    // ... add documents ...
    await context.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    // If blob storage: delete any blobs already uploaded in this batch
    throw;
}
```

### 4. Missing `ETag` / `If-Match` Header Support ✅ IMPLEMENTED

The concurrency check uses `RowVersion` from the **request body**, not from the `If-Match` HTTP header. Enterprise OData APIs expose `RowVersion` as an `ETag` header and require `If-Match` on mutations. Current pattern:

- Requires clients to embed `RowVersion` in the payload
- No `ETag` header on GET responses for cache validation

> **Status:** Implemented. The EDM model now declares `RowVersion` as a concurrency token via `.Property(d => d.RowVersion).IsConcurrencyToken()` in `BuildEdmModel()`. The OData serializer now emits `@odata.etag` on responses automatically.

**Recommended Fix:** Configure the OData EDM model to declare `RowVersion` as the concurrency token via `HasOptimisticConcurrency()`. This causes the OData serializer to emit `@odata.etag` and the runtime to read `If-Match` headers automatically. Override `CreateODataResult` or add an action filter to set the `ETag` response header:

```csharp
// In EDM model builder
var document = builder.EntitySet<LineOfDutyDocument>("Documents");
document.EntityType.Property(d => d.RowVersion).IsConcurrencyToken();

// In Patch/Put — read If-Match header
var ifMatch = Request.Headers.IfMatch;
if (ifMatch.Count == 0)
    return BadRequest("If-Match header with ETag is required.");
```

### 5. No Authorization Beyond Authentication

`[Authorize]` ensures the user is logged in, but there's no resource-level authorization — any authenticated user can read, update, or delete any document. Enterprise APIs enforce:

- Role-based access (`[Authorize(Roles = "CaseManager")]`)
- Resource ownership checks (user can only modify their case's documents)
- Claim-based or policy-based authorization

**Recommended Fix:** Add a resource-based authorization handler using ASP.NET Core's `IAuthorizationService`. Define a `DocumentOperationRequirement` and a handler that checks whether the current user owns (or is assigned to) the parent case:

```csharp
// Requirement
public class DocumentOperationRequirement : IAuthorizationRequirement { }

// Handler
public class DocumentAuthorizationHandler
    : AuthorizationHandler<DocumentOperationRequirement, LineOfDutyDocument>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        DocumentOperationRequirement requirement,
        LineOfDutyDocument resource)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // Check ownership or role
        if (context.User.IsInRole("Admin") || resource.CreatedBy == userId)
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

// In controller
var authResult = await _authorizationService.AuthorizeAsync(User, existing, new DocumentOperationRequirement());
if (!authResult.Succeeded) return Forbid();
```

Also add role-based endpoint restrictions: `[Authorize(Policy = "CanManageDocuments")]` on mutation endpoints.

### 6. Missing `Content-Type` Validation on Upload Response ✅ IMPLEMENTED

The `Upload` method trusts `IFormFile.ContentType` from the client without validating it matches the detected file type. An attacker could upload a PDF with `ContentType: text/html`, which could be served back with that type via `GetValue`, enabling stored XSS in browsers that honor the content type.

> **Status:** Implemented. A static `MimeMap` dictionary (13 extension→MIME entries) is defined on the controller. Upload now sets `ContentType = MimeMap.GetValueOrDefault(ext, "application/octet-stream")`, overriding the client-provided value.

**Recommended Fix:** Build an extension-to-MIME mapping and override the client-provided `ContentType`. On download, always set `X-Content-Type-Options: nosniff` to prevent browser MIME sniffing:

```csharp
private static readonly Dictionary<string, string> MimeMap = new(StringComparer.OrdinalIgnoreCase)
{
    { ".pdf",  "application/pdf" },
    { ".doc",  "application/msword" },
    { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
    { ".xls",  "application/vnd.ms-excel" },
    { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
    { ".jpg",  "image/jpeg" },
    { ".jpeg", "image/jpeg" },
    { ".png",  "image/png" },
    { ".gif",  "image/gif" },
    { ".tif",  "image/tiff" },
    { ".tiff", "image/tiff" },
    { ".txt",  "text/plain" },
    { ".rtf",  "application/rtf" },
};

// In Upload — override client ContentType
var ext = Path.GetExtension(f.FileName);
document.ContentType = MimeMap.GetValueOrDefault(ext, "application/octet-stream");

// In GetValue — add nosniff header
Response.Headers["X-Content-Type-Options"] = "nosniff";
```

### 7. No Rate Limiting or Throttling

No per-user or per-endpoint rate limiting. File uploads and PDF generation are expensive — unthrottled access allows resource exhaustion.

**Recommended Fix:** Use the built-in .NET rate limiter middleware (`Microsoft.AspNetCore.RateLimiting`). Define separate policies for reads vs. expensive operations:

```csharp
// In Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("upload", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 0;
    });
    options.AddFixedWindowLimiter("pdf", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueLimit = 2;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// On endpoints
[EnableRateLimiting("upload")]
public async Task<IActionResult> Upload(...)

[EnableRateLimiting("pdf")]
public async Task<IActionResult> GetForm348(...)
```

### 8. Download Loads Full Blob into Memory

`GetValue` loads `d.Content` into a byte array via EF, then passes to `File()`. For large documents, this is a GC-unfriendly allocation. Enterprise pattern: stream from blob storage.

**Recommended Fix:** With blob storage (see #1), return a pre-signed SAS URL redirect or stream from `BlobClient.OpenReadAsync()`. If the database remains the source short-term, use EF's streaming support to avoid loading the full byte array:

```csharp
// With blob storage — redirect to SAS URL
var sasUri = _blobService.GenerateSasUri(doc.BlobUri, TimeSpan.FromMinutes(5));
return Redirect(sasUri.ToString());

// Without blob storage — stream from DB using ADO.NET
var connection = context.Database.GetDbConnection();
await connection.OpenAsync(ct);
using var command = connection.CreateCommand();
command.CommandText = "SELECT Content FROM Documents WHERE Id = @id";
command.Parameters.Add(new SqlParameter("@id", key));
using var reader = await command.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
if (await reader.ReadAsync(ct))
{
    var stream = reader.GetStream(0);
    return File(stream, doc.ContentType, doc.FileName);
}
```

### 9. Error Responses Lack Problem Details ✅ IMPLEMENTED

Errors return bare `BadRequest("string")` or `Conflict()` without [RFC 9457](https://www.rfc-editor.org/rfc/rfc9457) Problem Details (`ProblemDetails` / `ValidationProblemDetails`). Enterprise APIs use a consistent error envelope for machine-parseable errors.

> **Status:** Implemented. All error responses across Patch, Put, Delete, GetValue, Upload, and GetForm348 now return `Problem(title:..., detail:..., statusCode:...)` or `ValidationProblem(ModelState)`. `AddProblemDetails()` registered in DI.

**Recommended Fix:** Enable the built-in Problem Details service and return typed responses. ASP.NET Core 7+ has first-class support:

```csharp
// In Program.cs
builder.Services.AddProblemDetails();

// In controller — replace bare strings with ProblemDetails
return Problem(
    title: "File type not permitted",
    detail: $"Extension '{extension}' is not in the allowed list.",
    statusCode: StatusCodes.Status400BadRequest,
    type: "https://ectsystem.mil/errors/invalid-file-type"
);

// For concurrency conflicts
return Problem(
    title: "Concurrency conflict",
    detail: "The document was modified by another user. Refresh and retry.",
    statusCode: StatusCodes.Status409Conflict,
    type: "https://ectsystem.mil/errors/concurrency-conflict"
);
```

### 10. No Audit Trail on Mutations ✅ IMPLEMENTED

Patch, Put, Delete, and Upload don't record who, when, or what changed. The `AuditableEntity` base has `CreatedBy`/`ModifiedBy` fields but no middleware or interceptor populates them.

> **Status:** Implemented. Created `AuditSaveChangesInterceptor` (in `ECTSystem.Api/Services/`) that reads `ClaimTypes.NameIdentifier` from `HttpContext.User` and populates `CreatedBy`/`CreatedDate` on Added, `ModifiedBy`/`ModifiedDate` on Added+Modified. Registered via `AddInterceptors()` on the pooled `DbContextFactory`. Removed the duplicate `SaveChangesAsync` override from `EctDbContext`.

**Recommended Fix:** Add a `SaveChangesInterceptor` that automatically populates `CreatedBy`, `ModifiedBy`, `CreatedDate`, and `ModifiedDate` from `HttpContext.User`. Register it in the `DbContext` configuration:

```csharp
public class AuditInterceptor : SaveChangesInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditInterceptor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var userId = _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "system";
        var now = DateTime.UtcNow;

        foreach (var entry in eventData.Context!.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedBy = userId;
                entry.Entity.CreatedDate = now;
            }
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.ModifiedBy = userId;
                entry.Entity.ModifiedDate = now;
            }
        }
        return base.SavingChangesAsync(eventData, result, ct);
    }
}

// In Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddDbContextFactory<EctDbContext>((sp, options) =>
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>()));
```

For delete auditing, consider soft-delete (`IsDeleted` flag) or a separate `AuditLog` table populated by the same interceptor.

---

## Enterprise-Grade Comparison

| Capability | This Controller | Enterprise Standard |
|---|---|---|
| File storage | Database `varbinary(max)` | Blob storage + metadata DB row |
| Streaming | Full buffer in RAM | Stream-through to storage |
| Auth | `[Authorize]` (authn only) | RBAC + resource ownership + policy |
| Concurrency | ✅ `ETag` via EDM concurrency token | `ETag` / `If-Match` headers |
| Error format | ✅ RFC 9457 Problem Details | RFC 9457 Problem Details |
| Rate limiting | None | Per-user / per-endpoint throttling |
| Audit | ✅ `AuditSaveChangesInterceptor` | Change feed / audit log table |
| Virus scanning | None | ClamAV / Defender scan on upload |
| Content-Type trust | ✅ Server-enforced via MimeMap | Server-detected + enforced |
| Idempotency | None | `Idempotency-Key` header for uploads |
| Observability | Structured logging | + distributed tracing + metrics |
| Caching | `NoStore` | `ETag`-based conditional GET |
| Upload consistency | ✅ Explicit transaction + FK check | Transactional with rollback |

---

## Bottom Line

The controller is **solid for its current scope** — correct OData patterns, good query protection, above-average file-type validation with magic-byte checks, proper concurrency, efficient deletes, and clean separation via base class. Five of the ten identified weaknesses have been implemented:

- ✅ #3 — Transactional consistency on multi-file upload
- ✅ #4 — ETag / `@odata.etag` via EDM concurrency token
- ✅ #6 — Server-side Content-Type enforcement via MimeMap
- ✅ #9 — RFC 9457 ProblemDetails on all error responses
- ✅ #10 — Audit trail via `AuditSaveChangesInterceptor`

Remaining high-impact improvements:

1. Move binary content to blob storage (#1)
2. Add resource-level authorization (#5)
3. Add rate limiting on upload/PDF endpoints (#7)
4. Stream downloads instead of buffering (#8)
