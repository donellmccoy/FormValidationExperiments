using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Deltas;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// OData-enabled controller for querying document metadata and managing uploads/downloads.
/// Named "DocumentsController" to match the OData entity set "Documents" (convention routing).
/// </summary>
[Authorize]
public class DocumentsController : ODataControllerBase
{
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".jpg", ".jpeg", ".png", ".gif", ".tif", ".tiff",
        ".txt", ".rtf"
    };

    private static readonly Dictionary<string, byte[][]> FileSignatures = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".pdf",  [new byte[] { 0x25, 0x50, 0x44, 0x46 }] },
        { ".jpg",  [new byte[] { 0xFF, 0xD8, 0xFF }] },
        { ".jpeg", [new byte[] { 0xFF, 0xD8, 0xFF }] },
        { ".png",  [new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }] },
        { ".gif",  [new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 },
                    new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }] },
        { ".tif",  [new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                    new byte[] { 0x4D, 0x4D, 0x00, 0x2A }] },
        { ".tiff", [new byte[] { 0x49, 0x49, 0x2A, 0x00 },
                    new byte[] { 0x4D, 0x4D, 0x00, 0x2A }] },
        { ".doc",  [new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }] },
        { ".xls",  [new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 }] },
        { ".docx", [new byte[] { 0x50, 0x4B, 0x03, 0x04 }] },
        { ".xlsx", [new byte[] { 0x50, 0x4B, 0x03, 0x04 }] },
    };

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

    private readonly AF348PdfService _pdfService;
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

    /// <summary>
    /// Returns an IQueryable of documents for OData query composition.
    /// OData route: GET /odata/Documents
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true)]     
    public async Task<IActionResult> Get(CancellationToken ct = default)        
    {
        LoggingService.QueryingDocuments();
        var context = await CreateContextAsync(ct);
        return Ok(context.Documents.AsNoTracking());
    }

    /// <summary>
    /// Returns a single document by key as a deferred IQueryable for full OData composition.
    /// OData route: GET /odata/Documents({key})
    /// </summary>
    /// <param name="key">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(NoStore = true)]     
    public async Task<SingleResult<LineOfDutyDocument>> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.RetrievingDocument(key, 0);
        var context = await CreateContextAsync(ct);
        var query = context.Documents.AsNoTracking().Where(d => d.Id == key);
        return SingleResult.Create(query);
    }

    /// <summary>
    /// Partially updates an existing document's metadata using OData Delta semantics.
    /// OData route: PATCH /odata/Documents({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Patch([FromODataUri] int key, Delta<LineOfDutyDocument> delta, CancellationToken ct = default)
    {
        if (delta is null || !ModelState.IsValid)
            return ValidationProblem(ModelState);

        LoggingService.PatchingDocument(key, 0);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Documents.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return Problem(
                title: "Document not found",
                detail: $"No document exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        delta.Patch(existing);

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = existing.RowVersion;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Concurrency conflict",
                detail: "The document was modified by another user. Refresh and retry.",
                statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.DocumentPatched(key, existing.LineOfDutyCaseId);
        return Updated(existing);
    }

    /// <summary>
    /// Fully replaces an existing document's metadata.
    /// OData route: PUT /odata/Documents({key})
    /// </summary>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    public async Task<IActionResult> Put([FromODataUri] int key, [FromBody] LineOfDutyDocument document, CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (key != document.Id)
            return Problem(
                title: "Key mismatch",
                detail: "The key parameter does not match the entity ID.",
                statusCode: StatusCodes.Status400BadRequest);

        LoggingService.UpdatingDocument(key, 0);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var existing = await context.Documents.FindAsync([key], ct);

        if (existing is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return Problem(
                title: "Document not found",
                detail: $"No document exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Use client-provided RowVersion for optimistic concurrency check
        context.Entry(existing).Property(e => e.RowVersion).OriginalValue = document.RowVersion;
        context.Entry(existing).CurrentValues.SetValues(document);

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Problem(
                title: "Concurrency conflict",
                detail: "The document was modified by another user. Refresh and retry.",
                statusCode: StatusCodes.Status409Conflict);
        }

        LoggingService.DocumentUpdated(key, existing.LineOfDutyCaseId);
        return Updated(existing);
    }

    /// <summary>
    /// Downloads the binary content of a document as a media stream ($value).
    /// OData route: GET /odata/Documents({key})/$value
    /// </summary>
    [HttpGet("odata/Documents({key})/$value")]
    public async Task<IActionResult> GetValue([FromRoute] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var doc = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
            .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId, d.BlobPath })
            .FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return Problem(
                title: "Document not found",
                detail: $"No document exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        LoggingService.DownloadingDocument(key, doc.LineOfDutyCaseId);

        if (string.IsNullOrEmpty(doc.BlobPath))
        {
            LoggingService.DocumentContentNotFound(key);
            return Problem(
                title: "Document content not found",
                detail: $"Document {key} exists but has no binary content.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var stream = await _blobStorage.OpenReadAsync(doc.BlobPath, ct);
        Response.Headers.ContentDisposition = $"attachment; filename=\"{doc.FileName}\"";
        return File(stream, doc.ContentType, doc.FileName);
    }

    /// <summary>
    /// Uploads one or more documents and associates them with the specified case.
    /// Acts as an OData navigation POST endpoint.
    /// Route: POST /odata/Cases({caseId})/Documents
    /// </summary>
    [HttpPost("odata/Cases({caseId})/Documents")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total for multiple files
    public async Task<IActionResult> Upload(
        [FromRoute] int caseId,
        List<IFormFile> file, // Expected by RadzenUpload
        [FromForm] string documentType = "Miscellaneous",
        [FromForm] string description = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Count == 0)
        {
            LoggingService.InvalidUpload(caseId);
            return Problem(
                title: "No file provided",
                detail: "At least one file must be included in the upload request.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        foreach (var f in file)
        {
            var extension = Path.GetExtension(f.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                LoggingService.InvalidUpload(caseId);
                return Problem(
                    title: "File type not permitted",
                    detail: $"Extension '{extension}' is not in the allowed list.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (f.Length > MaxDocumentSize)
            {
                LoggingService.InvalidUpload(caseId);
                return Problem(
                    title: "File too large",
                    detail: $"File '{f.FileName}' exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

            if (FileSignatures.TryGetValue(extension, out var signatures))
            {
                using var headerStream = f.OpenReadStream();
                var headerBytes = new byte[8];
                var bytesRead = await headerStream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length), ct);

                if (!signatures.Any(sig => sig.Length <= bytesRead && headerBytes.AsSpan(0, sig.Length).SequenceEqual(sig)))
                {
                    LoggingService.InvalidUpload(caseId);
                    return Problem(
                        title: "File content mismatch",
                        detail: $"File '{f.FileName}' content does not match the '{extension}' file type.",
                        statusCode: StatusCodes.Status400BadRequest);
                }
            }
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        // Validate the parent case exists before processing files
        if (!await context.Cases.AnyAsync(c => c.Id == caseId, ct))
        {
            LoggingService.InvalidUpload(caseId);
            return Problem(
                title: "Case not found",
                detail: $"No case exists with ID {caseId}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var strategy = context.Database.CreateExecutionStrategy();

        var documents = new List<LineOfDutyDocument>(file.Count);

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                documents.Clear();
                context.ChangeTracker.Clear();

                await using var transaction = await context.Database.BeginTransactionAsync(ct);

                foreach (var f in file)
                {
                    LoggingService.UploadingDocument(caseId);

                    var ext = Path.GetExtension(f.FileName);
                    var contentType = MimeMap.GetValueOrDefault(ext, "application/octet-stream");

                    // Stream directly to blob storage
                    string blobPath;
                    using (var stream = f.OpenReadStream())
                    {
                        blobPath = await _blobStorage.UploadAsync(stream, f.FileName, contentType, caseId, ct);
                    }

                    if (!Enum.TryParse<DocumentType>(documentType, ignoreCase: true, out var parsedDocType))
                    {
                        parsedDocType = DocumentType.Miscellaneous;
                    }

                    var document = new LineOfDutyDocument
                    {
                        LineOfDutyCaseId = caseId,
                        FileName = f.FileName,
                        ContentType = contentType,
                        DocumentType = parsedDocType,
                        Description = description,
                        BlobPath = blobPath,
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
            {
                LoggingService.DocumentUploaded(document.Id, caseId);
            }

            return Ok(documents);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LoggingService.UploadFailed(caseId, ex);
            throw;
        }
    }

    /// <summary>
    /// Deletes a document by its identifier.
    /// Standard OData route: DELETE /odata/Documents({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        LoggingService.DeletingDocument(key, 0);
        await using var context = await ContextFactory.CreateDbContextAsync(ct);

        var doc = await context.Documents
            .Where(d => d.Id == key)
            .Select(d => new { d.Id, d.BlobPath })
            .FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return Problem(
                title: "Document not found",
                detail: $"No document exists with ID {key}.",
                statusCode: StatusCodes.Status404NotFound);
        }

        await context.Documents
            .Where(d => d.Id == key)
            .ExecuteDeleteAsync(ct);

        // Best-effort blob cleanup — don't fail the request if blob delete fails
        if (!string.IsNullOrEmpty(doc.BlobPath))
        {
            try
            {
                await _blobStorage.DeleteAsync(doc.BlobPath, ct);
            }
            catch (Exception ex)
            {
                LoggingService.BlobDeleteFailed(doc.BlobPath, key, ex.Message);
            }
        }

        LoggingService.DocumentDeleted(key, 0);
        return NoContent();
    }

    /// <summary>
    /// Generates a filled AF Form 348 PDF for the specified case.
    /// Custom hybrid route: GET /odata/Cases({caseId})/Form348
    /// </summary>
    [HttpGet("odata/Cases({caseId})/Form348")]
    public async Task<IActionResult> GetForm348([FromRoute] int caseId, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var lodCase = await context.Cases.AsNoTracking()
            .Include(c => c.Member)
            .FirstOrDefaultAsync(c => c.Id == caseId, ct);

        if (lodCase is null)
            return Problem(
                title: "Case not found",
                detail: $"No case exists with ID {caseId}.",
                statusCode: StatusCodes.Status404NotFound);

        var pdfBytes = _pdfService.GenerateFilledForm(lodCase);
        return File(pdfBytes, "application/pdf", $"AF348_{lodCase.CaseId}.pdf");
}
}
