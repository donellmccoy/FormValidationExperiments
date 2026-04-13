# D.3 — Move Binary Document Storage to Azure Blob Storage

> **Source:** Controller Remediation Plan, Deferred Item D.3
> **Scope:** `DocumentsController.cs`, `LineOfDutyDocument.cs`, new `IBlobStorageService`, EF migration, client service updates
> **Rationale:** Eliminates database bloat, memory pressure on upload/download, and enables CDN caching
> **References:** Documents characterization — Weakness #1 (binary in DB), #2 (memory buffering), #8 (no CDN tier)

---

## Table of Contents

1. [Current State Analysis](#1-current-state-analysis)
2. [Target Architecture](#2-target-architecture)
3. [Implementation Phases](#3-implementation-phases)
   - [Phase 1 — Infrastructure & Abstractions](#phase-1--infrastructure--abstractions)
   - [Phase 2 — API Refactoring](#phase-2--api-refactoring)
   - [Phase 3 — Data Migration](#phase-3--data-migration)
   - [Phase 4 — Client Updates](#phase-4--client-updates)
   - [Phase 5 — Cleanup & Optimization](#phase-5--cleanup--optimization)
4. [Configuration](#4-configuration)
5. [Security Considerations](#5-security-considerations)
6. [Testing Strategy](#6-testing-strategy)
7. [Rollback Plan](#7-rollback-plan)
8. [Risks & Mitigations](#8-risks--mitigations)

---

## 1. Current State Analysis

### Model

`LineOfDutyDocument` (inherits `AuditableEntity`) stores binary content inline:

```csharp
// ECTSystem.Shared/Models/LineOfDutyDocument.cs
public byte[] Content { get; set; } = Array.Empty<byte>(); // varbinary(max)
public string FileName { get; set; }
public string ContentType { get; set; }  // MIME type
public long FileSize { get; set; }
```

### EF Configuration

```csharp
// ECTSystem.Persistence/Data/Configurations/LineOfDutyDocumentConfiguration.cs
builder.Property(e => e.Content).HasColumnType("varbinary(max)");
```

### Upload Path (Current)

1. Client reads file into `byte[]` → sends as `MultipartFormDataContent`
2. `DocumentsController.Upload` receives `List<IFormFile>`
3. Each file is copied to `MemoryStream` → `ToArray()` → stored in `LineOfDutyDocument.Content`
4. EF saves the full byte array to `varbinary(max)` in SQL Server

**Problem:** A 10 MB file allocates 10 MB on the managed heap (Large Object Heap), then EF allocates a second copy for the SQL parameter. A 50 MB batch (5 × 10 MB) causes ~100 MB of LOH allocations per request.

### Download Path (Current)

1. `DocumentsController.GetValue` projects `d.Content` from the database
2. SQL Server reads the entire `varbinary(max)` value into application memory
3. `File(doc.Content, ...)` writes the byte array to the response stream

**Problem:** The entire file is buffered in server memory before any bytes reach the client. No streaming, no CDN, no SAS-based direct download.

### Client Service

```csharp
// ECTSystem.Web/Services/DocumentService.cs
Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, ...);
Task DeleteDocumentAsync(int caseId, int documentId, ...);
Task<byte[]> GetForm348PdfAsync(int caseId, ...);  // PDF generation (unchanged by this migration)
```

Metadata queries already exclude `Content` via `$select` — no change needed for list/grid views.

---

## 2. Target Architecture

```
┌──────────────────┐       ┌───────────────────────┐       ┌────────────────────┐
│  Blazor WASM     │       │  ASP.NET Core API      │       │  Azure Blob Storage │
│  (DocumentService)│──────▶│  DocumentsController   │──────▶│  lod-documents      │
│                  │       │  IBlobStorageService    │       │  (container)        │
└──────────────────┘       └───────────────────────┘       └────────────────────┘
                                    │
                                    │ metadata only
                                    ▼
                           ┌───────────────────────┐
                           │  SQL Server            │
                           │  Documents table       │
                           │  (no Content column)   │
                           └───────────────────────┘
```

### Key Changes

| Aspect | Before | After |
|--------|--------|-------|
| Binary storage | `varbinary(max)` in SQL Server | Azure Blob Storage container |
| Upload flow | File → `MemoryStream` → EF → SQL | File → `BlobClient.UploadAsync(stream)` |
| Download flow | SQL → `byte[]` → `File()` | SAS URL redirect **or** `BlobClient.OpenReadAsync()` → stream |
| Model property | `byte[] Content` | `string BlobPath` (relative blob path) |
| Memory pressure | Full file buffered in RAM | Streamed end-to-end |
| CDN support | None | Azure CDN on Blob Storage endpoint |

---

## 3. Implementation Phases

### Phase 1 — Infrastructure & Abstractions

**Goal:** Introduce the blob storage service abstraction, Azure implementation, and local development fallback without changing any existing behavior.

#### Step 1.1 — Define `IBlobStorageService` in `ECTSystem.Api`

**File:** `ECTSystem.Api/Services/IBlobStorageService.cs` (new)

```csharp
namespace ECTSystem.Api.Services;

/// <summary>
/// Abstraction for binary document storage.
/// Production: Azure Blob Storage. Development: Azurite or local file system.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>
    /// Uploads a file stream to blob storage and returns the relative blob path.
    /// </summary>
    /// <param name="stream">The file content stream.</param>
    /// <param name="fileName">The original file name (used for content-disposition, not the blob name).</param>
    /// <param name="contentType">The MIME content type.</param>
    /// <param name="caseId">The parent case ID (used as a virtual directory prefix).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The relative blob path (e.g., "cases/42/a1b2c3d4-report.pdf").</returns>
    Task<string> UploadAsync(Stream stream, string fileName, string contentType, int caseId, CancellationToken ct = default);

    /// <summary>
    /// Opens a read-only stream to the blob content.
    /// Use for proxy-streaming downloads through the API.
    /// </summary>
    Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Deletes a blob by its relative path.
    /// </summary>
    Task DeleteAsync(string blobPath, CancellationToken ct = default);

    /// <summary>
    /// Generates a time-limited read-only SAS URI for direct client download.
    /// </summary>
    /// <param name="blobPath">The relative blob path.</param>
    /// <param name="fileName">The original file name for Content-Disposition.</param>
    /// <param name="expiry">How long the SAS URI should be valid.</param>
    /// <returns>A fully-qualified SAS URI.</returns>
    Uri GenerateDownloadSasUri(string blobPath, string fileName, TimeSpan expiry);

    /// <summary>
    /// Deletes multiple blobs by their relative paths (batch cleanup).
    /// </summary>
    Task DeleteBatchAsync(IEnumerable<string> blobPaths, CancellationToken ct = default);
}
```

#### Step 1.2 — Implement `AzureBlobStorageService`

**File:** `ECTSystem.Api/Services/AzureBlobStorageService.cs` (new)

**NuGet dependency:** `Azure.Storage.Blobs` (add to `ECTSystem.Api.csproj`)

```csharp
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace ECTSystem.Api.Services;

public sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobContainerClient _container;

    public AzureBlobStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
    {
        var containerName = configuration.GetValue<string>("BlobStorage:ContainerName") ?? "lod-documents";
        _container = blobServiceClient.GetBlobContainerClient(containerName);
    }

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, int caseId, CancellationToken ct)
    {
        // Generate a unique blob name: cases/{caseId}/{guid}-{sanitizedFileName}
        var sanitized = Path.GetFileName(fileName); // strip directory traversal
        var blobName = $"cases/{caseId}/{Guid.NewGuid():N}-{sanitized}";
        var blobClient = _container.GetBlobClient(blobName);

        var headers = new BlobHttpHeaders { ContentType = contentType };
        await blobClient.UploadAsync(stream, new BlobUploadOptions { HttpHeaders = headers }, ct);

        return blobName;
    }

    public async Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        return await blobClient.OpenReadAsync(cancellationToken: ct);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(cancellationToken: ct);
    }

    public Uri GenerateDownloadSasUri(string blobPath, string fileName, TimeSpan expiry)
    {
        var blobClient = _container.GetBlobClient(blobPath);
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            ExpiresOn = DateTimeOffset.UtcNow.Add(expiry),
            ContentDisposition = $"attachment; filename=\"{fileName}\""
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder);
    }

    public async Task DeleteBatchAsync(IEnumerable<string> blobPaths, CancellationToken ct)
    {
        // Azure Blob Storage batch delete (up to 256 per batch)
        foreach (var path in blobPaths)
        {
            await DeleteAsync(path, ct);
        }
    }
}
```

#### Step 1.3 — Register Services in DI

**File:** `ECTSystem.Api/Extensions/ServiceCollectionExtensions.cs` (modify)

```csharp
using Azure.Storage.Blobs;

// In AddApplicationServices():
services.AddSingleton(_ =>
{
    var connectionString = configuration.GetConnectionString("BlobStorage")
        ?? "UseDevelopmentStorage=true"; // Azurite for local dev
    return new BlobServiceClient(connectionString);
});

services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();

// Ensure the blob container exists at startup
services.AddHostedService<BlobContainerInitializer>();
```

#### Step 1.4 — Container Initialization Hosted Service

**File:** `ECTSystem.Api/Services/BlobContainerInitializer.cs` (new)

```csharp
using Azure.Storage.Blobs;

namespace ECTSystem.Api.Services;

/// <summary>
/// Ensures the blob container exists when the API starts up.
/// </summary>
public sealed class BlobContainerInitializer : IHostedService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<BlobContainerInitializer> _logger;

    public BlobContainerInitializer(
        BlobServiceClient blobServiceClient,
        IConfiguration configuration,
        ILogger<BlobContainerInitializer> logger)
    {
        _blobServiceClient = blobServiceClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var containerName = _configuration.GetValue<string>("BlobStorage:ContainerName") ?? "lod-documents";
        var container = _blobServiceClient.GetBlobContainerClient(containerName);
        var created = await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

        if (created?.Value != null)
            _logger.LogInformation("Created blob container '{Container}'", containerName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

---

### Phase 2 — API Refactoring

**Goal:** Modify `DocumentsController` to use blob storage for upload/download while keeping the database for metadata only.

#### Step 2.1 — Add `BlobPath` property to the model

**File:** `ECTSystem.Shared/Models/LineOfDutyDocument.cs` (modify)

```csharp
public class LineOfDutyDocument : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime? UploadDate { get; set; }
    public string Description { get; set; } = string.Empty;

    // --- New: blob storage path (replaces Content) ---
    /// <summary>
    /// Relative path to the blob in Azure Blob Storage (e.g., "cases/42/a1b2c3d4-report.pdf").
    /// Null for legacy rows that still have inline Content (pre-migration).
    /// </summary>
    public string? BlobPath { get; set; }

    // --- Deprecated: will be removed after data migration ---
    /// <summary>
    /// [DEPRECATED] Inline binary content. Retained only for backward compatibility during
    /// the data migration period. New uploads set BlobPath instead.
    /// </summary>
    public byte[]? Content { get; set; }
}
```

#### Step 2.2 — Update EF Configuration

**File:** `ECTSystem.Persistence/Data/Configurations/LineOfDutyDocumentConfiguration.cs` (modify)

```csharp
public void Configure(EntityTypeBuilder<LineOfDutyDocument> builder)
{
    builder.HasKey(e => e.Id);
    builder.HasIndex(e => e.LineOfDutyCaseId);

    // Blob storage path — new column
    builder.Property(e => e.BlobPath).HasMaxLength(1024);

    // Content — retained during migration, will be dropped in Phase 3
    builder.Property(e => e.Content).HasColumnType("varbinary(max)");

    builder.Property(e => e.ContentType).HasMaxLength(256);
    builder.Property(e => e.FileName).HasMaxLength(512);
    builder.Property(e => e.DocumentType).HasMaxLength(100);
    builder.Property(e => e.Description).HasMaxLength(1000);
}
```

#### Step 2.3 — EF Migration: Add `BlobPath` column

```bash
dotnet ef migrations add AddDocumentBlobPath --project ECTSystem.Persistence --startup-project ECTSystem.Api
```

This adds a nullable `nvarchar(1024)` column. The `Content` column remains — it will be dropped later after data migration.

#### Step 2.4 — Refactor `Upload` to stream to blob storage

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (modify)

Replace the current upload body with:

```csharp
// Inject IBlobStorageService via constructor
private readonly IBlobStorageService _blobStorage;

public DocumentsController(
    IDbContextFactory<EctDbContext> contextFactory,
    ILoggingService loggingService,
    AF348PdfService pdfService,
    IBlobStorageService blobStorage)
    : base(contextFactory, loggingService)
{
    _pdfService = pdfService;
    _blobStorage = blobStorage;
}

[HttpPost("odata/Cases({caseId})/Documents")]
[RequestSizeLimit(50 * 1024 * 1024)]
public async Task<IActionResult> Upload(
    [FromRoute] int caseId,
    List<IFormFile> file,
    [FromForm] string documentType = "Supporting Document",
    [FromForm] string description = "",
    CancellationToken ct = default)
{
    // ... existing validation (extension, size, signature checks) ...

    await using var context = await ContextFactory.CreateDbContextAsync(ct);

    if (!await context.Cases.AnyAsync(c => c.Id == caseId, ct))
        return Problem(title: "Case not found", detail: $"No case exists with ID {caseId}.",
            statusCode: StatusCodes.Status404NotFound);

    var strategy = context.Database.CreateExecutionStrategy();
    var documents = new List<LineOfDutyDocument>(file.Count);
    var uploadedBlobs = new List<string>(); // Track for rollback

    try
    {
        await strategy.ExecuteAsync(async () =>
        {
            documents.Clear();
            uploadedBlobs.Clear();
            context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            foreach (var f in file)
            {
                LoggingService.UploadingDocument(caseId);

                var ext = Path.GetExtension(f.FileName);

                // Stream directly to blob storage — no MemoryStream buffer
                await using var stream = f.OpenReadStream();
                var blobPath = await _blobStorage.UploadAsync(
                    stream, f.FileName, MimeMap.GetValueOrDefault(ext, "application/octet-stream"),
                    caseId, ct);
                uploadedBlobs.Add(blobPath);

                var document = new LineOfDutyDocument
                {
                    LineOfDutyCaseId = caseId,
                    FileName = f.FileName,
                    ContentType = MimeMap.GetValueOrDefault(ext, "application/octet-stream"),
                    DocumentType = documentType,
                    Description = description,
                    BlobPath = blobPath,
                    Content = null,  // No longer stored in DB
                    FileSize = f.Length,
                    UploadDate = DateTime.UtcNow
                };

                context.Documents.Add(document);
                documents.Add(document);
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });

        foreach (var document in documents)
            LoggingService.DocumentUploaded(document.Id, caseId);

        return Ok(documents);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        // Compensating action: delete any blobs that were uploaded before the failure
        foreach (var blobPath in uploadedBlobs)
        {
            try { await _blobStorage.DeleteAsync(blobPath, CancellationToken.None); }
            catch { /* best-effort cleanup */ }
        }

        LoggingService.UploadFailed(caseId, ex);
        throw;
    }
}
```

**Key improvements:**
- **Zero intermediate buffer** — `IFormFile.OpenReadStream()` streams directly to blob storage
- **Compensating action** — uploaded blobs are deleted if the DB transaction fails
- **No LOH allocations** — no `MemoryStream` + `ToArray()` pattern

#### Step 2.5 — Refactor `GetValue` (Download) to stream from blob or use SAS redirect

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (modify)

```csharp
[HttpGet("odata/Documents({key})/$value")]
public async Task<IActionResult> GetValue([FromRoute] int key, CancellationToken ct = default)
{
    await using var context = await ContextFactory.CreateDbContextAsync(ct);
    var doc = await context.Documents
        .AsNoTracking()
        .Where(d => d.Id == key)
        .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId, d.BlobPath, d.Content })
        .FirstOrDefaultAsync(ct);

    if (doc is null)
    {
        LoggingService.DocumentNotFound(key, 0);
        return Problem(title: "Document not found", detail: $"No document exists with ID {key}.",
            statusCode: StatusCodes.Status404NotFound);
    }

    LoggingService.DownloadingDocument(key, doc.LineOfDutyCaseId);

    // New path: blob storage
    if (!string.IsNullOrEmpty(doc.BlobPath))
    {
        // Option A: SAS redirect (preferred — offloads bandwidth to blob storage)
        var sasUri = _blobStorage.GenerateDownloadSasUri(doc.BlobPath, doc.FileName, TimeSpan.FromMinutes(5));
        return Redirect(sasUri.ToString());

        // Option B: Proxy stream (if SAS is not acceptable for security policy)
        // var stream = await _blobStorage.OpenReadAsync(doc.BlobPath, ct);
        // return File(stream, doc.ContentType, doc.FileName);
    }

    // Legacy fallback: inline Content (pre-migration rows)
    if (doc.Content is null || doc.Content.Length == 0)
    {
        LoggingService.DocumentContentNotFound(key);
        return Problem(title: "Document content not found",
            detail: $"Document {key} exists but has no binary content.",
            statusCode: StatusCodes.Status404NotFound);
    }

    Response.Headers.ContentDisposition = $"attachment; filename=\"{doc.FileName}\"";
    return File(doc.Content, doc.ContentType, doc.FileName);
}
```

#### Step 2.6 — Refactor `Delete` to also delete the blob

**File:** `ECTSystem.Api/Controllers/DocumentsController.cs` (modify)

```csharp
public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
{
    LoggingService.DeletingDocument(key, 0);
    await using var context = await ContextFactory.CreateDbContextAsync(ct);

    // Fetch BlobPath before deleting the metadata row
    var blobPath = await context.Documents
        .Where(d => d.Id == key)
        .Select(d => d.BlobPath)
        .FirstOrDefaultAsync(ct);

    var deleted = await context.Documents
        .Where(d => d.Id == key)
        .ExecuteDeleteAsync(ct);

    if (deleted == 0)
    {
        LoggingService.DocumentNotFound(key, 0);
        return Problem(title: "Document not found", detail: $"No document exists with ID {key}.",
            statusCode: StatusCodes.Status404NotFound);
    }

    // Delete blob after DB row is gone (eventual consistency)
    if (!string.IsNullOrEmpty(blobPath))
    {
        try { await _blobStorage.DeleteAsync(blobPath, ct); }
        catch (Exception ex)
        {
            // Log but don't fail the request — orphan blob can be cleaned up later
            LoggingService.LogWarning("Failed to delete blob {BlobPath} for document {DocumentId}: {Error}",
                blobPath, key, ex.Message);
        }
    }

    LoggingService.DocumentDeleted(key, 0);
    return NoContent();
}
```

---

### Phase 3 — Data Migration

**Goal:** Migrate existing `varbinary(max)` content rows to blob storage, then drop the `Content` column.

#### Step 3.1 — Create a one-time migration command/tool

**File:** `ECTSystem.Api/Services/DocumentMigrationService.cs` (new)

This is a hosted service or CLI tool that:

1. Queries documents where `Content IS NOT NULL AND BlobPath IS NULL`
2. For each document, streams `Content` to blob storage via `IBlobStorageService.UploadAsync`
3. Sets `BlobPath` on the document and nulls out `Content`
4. Saves in batches (e.g., 50 documents per batch)

```csharp
namespace ECTSystem.Api.Services;

public sealed class DocumentMigrationService
{
    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<DocumentMigrationService> _logger;

    public DocumentMigrationService(
        IDbContextFactory<EctDbContext> contextFactory,
        IBlobStorageService blobStorage,
        ILogger<DocumentMigrationService> logger)
    {
        _contextFactory = contextFactory;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken ct = default)
    {
        const int batchSize = 50;
        var totalMigrated = 0;

        while (!ct.IsCancellationRequested)
        {
            await using var context = await _contextFactory.CreateDbContextAsync(ct);

            var batch = await context.Documents
                .Where(d => d.Content != null && d.BlobPath == null)
                .OrderBy(d => d.Id)
                .Take(batchSize)
                .ToListAsync(ct);

            if (batch.Count == 0)
                break;

            foreach (var doc in batch)
            {
                using var stream = new MemoryStream(doc.Content!);
                var blobPath = await _blobStorage.UploadAsync(
                    stream, doc.FileName, doc.ContentType, doc.LineOfDutyCaseId, ct);

                doc.BlobPath = blobPath;
                doc.Content = null;

                _logger.LogInformation(
                    "Migrated document {DocumentId} for case {CaseId} to blob {BlobPath}",
                    doc.Id, doc.LineOfDutyCaseId, blobPath);
            }

            await context.SaveChangesAsync(ct);
            totalMigrated += batch.Count;

            _logger.LogInformation("Migrated {Count} documents so far", totalMigrated);
        }

        _logger.LogInformation("Migration complete. Total documents migrated: {Total}", totalMigrated);
    }
}
```

**Trigger options:**
- Expose as a protected API endpoint: `POST /api/admin/migrate-documents` (requires Admin role)
- Run as a one-time `IHostedService` gated by a config flag
- Run as a standalone CLI tool

#### Step 3.2 — Drop the `Content` column (after migration is verified)

**Only after confirming all rows have `BlobPath` set and `Content` is `NULL`:**

```bash
dotnet ef migrations add DropDocumentContentColumn --project ECTSystem.Persistence --startup-project ECTSystem.Api
```

Manual migration edit:

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // Safety: fail if any rows still have Content but no BlobPath
    migrationBuilder.Sql(
        "IF EXISTS (SELECT 1 FROM Documents WHERE Content IS NOT NULL AND BlobPath IS NULL) " +
        "THROW 50000, 'Cannot drop Content — unmigrated documents exist.', 1;");

    migrationBuilder.DropColumn(name: "Content", table: "Documents");
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    migrationBuilder.AddColumn<byte[]>(
        name: "Content",
        table: "Documents",
        type: "varbinary(max)",
        nullable: true);
}
```

#### Step 3.3 — Remove `Content` property from the model

**File:** `ECTSystem.Shared/Models/LineOfDutyDocument.cs` (modify — after Phase 3.2 migration runs)

Remove the `Content` property entirely. Update `BlobPath` from `string?` to `string` (required).

---

### Phase 4 — Client Updates

#### Step 4.1 — Update the OData Client EDM Model

**File:** `ECTSystem.Web/Extensions/ServiceCollectionExtensions.cs`

In `BuildClientEdmModel()`, add the `BlobPath` property to the `LineOfDutyDocument` entity type and remove `Content`:

```csharp
documentType.AddStructuralProperty("BlobPath", EdmPrimitiveTypeKind.String);
// Remove: documentType.AddStructuralProperty("Content", EdmPrimitiveTypeKind.Binary);
```

#### Step 4.2 — Update `DocumentService` (no breaking changes expected)

The client `DocumentService` already:
- Excludes `Content` from metadata queries via `$select`
- Uses `HttpClient.PostAsync` for upload (multipart form — unchanged)
- Delete goes through OData context (unchanged)

The download flow (`GetValue`/$value endpoint) now returns a `302 redirect` to a SAS URL instead of raw bytes. For Blazor WASM, use `window.open()` or an anchor tag with the download URL:

**File:** `ECTSystem.Web/Services/DocumentService.cs` (modify)

Add a method to get the download URL instead of streaming bytes in-browser:

```csharp
/// <summary>
/// Returns the direct download URL for a document.
/// The server responds with a 302 redirect to a time-limited SAS URL.
/// </summary>
public string GetDocumentDownloadUrl(int documentId)
{
    return $"{HttpClient.BaseAddress}odata/Documents({documentId})/$value";
}
```

**File:** `ECTSystem.Web/Pages/EditCase.Documents.razor.cs` (modify)

For download, use `NavigationManager` or JS interop to open the URL in a new tab:

```csharp
// Instead of fetching bytes and creating a blob URL:
await JSRuntime.InvokeVoidAsync("open", DocumentService.GetDocumentDownloadUrl(documentId), "_blank");
```

#### Step 4.3 — Update `$select` projections

If client queries explicitly select `Content`, update them to select `BlobPath` instead. Current metadata queries already exclude `Content`, so this is likely a no-op.

---

### Phase 5 — Cleanup & Optimization

| Item | Description |
|------|-------------|
| 5.1 | Remove `Content` property from `LineOfDutyDocument` (after Phase 3 migration completes) |
| 5.2 | Remove legacy `Content` fallback from `GetValue` (after all rows migrated) |
| 5.3 | Remove `DocumentMigrationService` (one-time tool) |
| 5.4 | Update `LineOfDutyDocumentConfiguration` — remove `varbinary(max)` config line |
| 5.5 | Update test document builders (`BuildDocument()`) — use `BlobPath` instead of `Content` |
| 5.6 | Update OData EDM model in `ServiceCollectionExtensions.cs` — remove `Content` from entity type definition |
| 5.7 | Consider enabling Azure CDN on the blob storage endpoint for download performance |

---

## 4. Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "EctDatabase": "...",
    "BlobStorage": "UseDevelopmentStorage=true"
  },
  "BlobStorage": {
    "ContainerName": "lod-documents"
  }
}
```

### appsettings.Production.json

```json
{
  "ConnectionStrings": {
    "BlobStorage": "DefaultEndpointsProtocol=https;AccountName=ectstorage;AccountKey=...;EndpointSuffix=core.windows.net"
  },
  "BlobStorage": {
    "ContainerName": "lod-documents"
  }
}
```

### Alternative: Managed Identity (recommended for production)

Instead of connection strings with account keys, use `DefaultAzureCredential`:

```csharp
services.AddSingleton(_ =>
{
    var storageUri = new Uri(configuration["BlobStorage:ServiceUri"]!); // https://ectstorage.blob.core.windows.net
    return new BlobServiceClient(storageUri, new Azure.Identity.DefaultAzureCredential());
});
```

```json
{
  "BlobStorage": {
    "ServiceUri": "https://ectstorage.blob.core.windows.net",
    "ContainerName": "lod-documents"
  }
}
```

### Local Development: Azurite

Use the [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) emulator for local development. The default connection string `UseDevelopmentStorage=true` connects to Azurite automatically.

```bash
# Install globally
npm install -g azurite

# Start
azurite --silent --location ./azurite-data --debug ./azurite-debug.log
```

Or use the VS Code Azurite extension, or run via Docker:

```bash
docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
```

---

## 5. Security Considerations

| Concern | Mitigation |
|---------|------------|
| **Blob access** | Container access level: `Private` (no anonymous access). All access via SAS tokens or Managed Identity. |
| **SAS token scope** | Read-only, single-blob, 5-minute expiry. Regenerate per download request. |
| **SAS token leakage** | Short expiry (5 min) limits exposure. HTTPS only. Consider user-delegation SAS keys with `DefaultAzureCredential` for audit trail. |
| **Directory traversal** | Blob names are server-generated (`Guid`-prefixed). Client-supplied filenames are sanitized via `Path.GetFileName()`. Original filenames go in Content-Disposition header only. |
| **Malicious content** | Existing magic-byte validation and extension allowlist remain. Blob Storage adds no bypass vector. |
| **Credential management** | Use Managed Identity in production. No storage account keys in source control. Use Azure Key Vault for connection strings. |
| **CORS** | If using SAS redirect, the Blazor WASM client opens the URL in a new tab (no CORS issue). If proxy-streaming, no change needed. |
| **Data at rest** | Azure Blob Storage encrypts data at rest by default (SSE with Microsoft-managed keys). Optional: customer-managed keys via Key Vault. |

---

## 6. Testing Strategy

### Unit Tests

| Test | What |
|------|------|
| `AzureBlobStorageService_Upload_ReturnsValidBlobPath` | Verify blob path format: `cases/{caseId}/{guid}-{fileName}` |
| `AzureBlobStorageService_Upload_StreamsContent` | Verify content is readable after upload (use Azurite) |
| `AzureBlobStorageService_Delete_RemovesBlob` | Verify blob is gone after delete |
| `AzureBlobStorageService_GenerateSasUri_ReturnsValidUri` | Verify SAS URI has correct permissions and expiry |
| `DocumentsController_Upload_StoresInBlobNotDatabase` | Verify `BlobPath` is set and `Content` is null |
| `DocumentsController_Upload_DeletesBlobsOnDbFailure` | Verify compensating action on transaction rollback |
| `DocumentsController_GetValue_RedirectsToSasUri` | Verify 302 response when `BlobPath` is set |
| `DocumentsController_GetValue_FallsBackToContent` | Verify legacy inline content still works |
| `DocumentsController_Delete_DeletesBlobAndRow` | Verify both blob and DB row are removed |

### Integration Tests

| Test | What |
|------|------|
| Full upload → download cycle via Azurite | End-to-end with real blob storage emulator |
| Migration tool: `Content` → blob → verify | Run `DocumentMigrationService.MigrateAsync` against seeded DB + Azurite |
| Concurrent uploads to same case | Verify no blob name collisions (GUID prefix ensures uniqueness) |

### Test Infrastructure

Update `DocumentsControllerTests.cs` to inject a test double for `IBlobStorageService`:

```csharp
// In-memory blob storage for unit tests
public class InMemoryBlobStorageService : IBlobStorageService
{
    private readonly Dictionary<string, (byte[] Content, string ContentType)> _blobs = new();

    public async Task<string> UploadAsync(Stream stream, string fileName, string contentType, int caseId, CancellationToken ct)
    {
        var blobPath = $"cases/{caseId}/{Guid.NewGuid():N}-{Path.GetFileName(fileName)}";
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        _blobs[blobPath] = (ms.ToArray(), contentType);
        return blobPath;
    }

    public Task<Stream> OpenReadAsync(string blobPath, CancellationToken ct)
    {
        if (!_blobs.TryGetValue(blobPath, out var blob))
            throw new FileNotFoundException($"Blob not found: {blobPath}");
        return Task.FromResult<Stream>(new MemoryStream(blob.Content));
    }

    public Task DeleteAsync(string blobPath, CancellationToken ct)
    {
        _blobs.Remove(blobPath);
        return Task.CompletedTask;
    }

    public Uri GenerateDownloadSasUri(string blobPath, string fileName, TimeSpan expiry)
        => new($"https://test.blob.core.windows.net/lod-documents/{blobPath}?sig=test&se={DateTime.UtcNow.Add(expiry):o}");

    public Task DeleteBatchAsync(IEnumerable<string> blobPaths, CancellationToken ct)
    {
        foreach (var path in blobPaths)
            _blobs.Remove(path);
        return Task.CompletedTask;
    }
}
```

---

## 7. Rollback Plan

| Phase | Rollback Strategy |
|-------|-------------------|
| **Phase 1** (infra) | Remove new files. No DB changes, no data impact. |
| **Phase 2** (API refactor) | Revert controller changes. New documents uploaded during Phase 2 have both `BlobPath` and `Content = null` — they'd need manual re-upload or a reverse migration that downloads from blob → populates `Content`. |
| **Phase 3.1** (data migration) | Documents that were migrated had `Content` nulled. Reverse migration: download blob content → repopulate `Content` column. Blobs are **not** deleted during forward migration, so content is in both places until Phase 3.2. |
| **Phase 3.2** (drop column) | `Down` migration re-adds the `Content` column (empty). Would require re-downloading blobs into the column. **This is the point of no trivial rollback.** |

**Recommendation:** Keep Phase 3.1 and 3.2 in separate deployments with a bake period (e.g., 2 weeks) between them. During the bake period, both `Content` and `BlobPath` coexist, and the API's `GetValue` handles both paths.

---

## 8. Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Azurite not available for local dev | Low | Medium | Azurite is well-supported; fallback to `InMemoryBlobStorageService` for unit tests |
| SAS URL expiry causes download failures | Medium | Low | 5-minute window is generous for a download redirect. If needed, increase to 15 min. |
| Blob orphans (DB row deleted, blob remains) | Medium | Low | Scheduled cleanup job queries blobs without matching DB rows. Log orphans in delete catch block. |
| Network latency to blob storage on upload | Low | Medium | Blob storage in the same Azure region as the API. Upload streams directly — no double-buffering. |
| Migration tool fails midway | Medium | Low | Idempotent: queries `WHERE Content IS NOT NULL AND BlobPath IS NULL`. Safe to re-run. |
| Breaking change for client download (302 redirect) | Medium | Medium | Blazor WASM download already opens a new tab/window. 302 redirects are transparent to `window.open()`. Test browser compatibility. |
| Existing tests hardcoded to `Content` byte array | High | Low | Update test helpers in Phase 2. `InMemoryBlobStorageService` makes this straightforward. |

---

## NuGet Dependencies

| Package | Project | Purpose |
|---------|---------|---------|
| `Azure.Storage.Blobs` | `ECTSystem.Api` | Blob storage SDK |
| `Azure.Identity` | `ECTSystem.Api` | Managed Identity auth (production) |

---

## Implementation Order Summary

| Phase | Items | Effort | Dependencies | Risk |
|-------|-------|--------|-------------|------|
| **1 — Infrastructure** | 1.1–1.4 | Small | None | Low — additive, no behavior change |
| **2 — API Refactor** | 2.1–2.6 | Medium | Phase 1 | Medium — changes upload/download behavior |
| **3 — Data Migration** | 3.1–3.3 | Medium | Phase 2 deployed | Medium — moves production data |
| **4 — Client Updates** | 4.1–4.3 | Small | Phase 2 deployed | Low — download URL change |
| **5 — Cleanup** | 5.1–5.7 | Small | Phase 3 verified | Low — removes dead code |
