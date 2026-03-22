using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
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

    private readonly AF348PdfService _pdfService;

    public DocumentsController(
        IDbContextFactory<EctDbContext> contextFactory, 
        ILoggingService loggingService,
        AF348PdfService pdfService)
        : base(contextFactory, loggingService)
    {
        _pdfService = pdfService;
    }

    /// <summary>
    /// Returns an IQueryable of documents for OData query composition.
    /// OData route: GET /odata/Documents
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]     
    public async Task<IActionResult> Get(CancellationToken ct = default)        
    {
        LoggingService.QueryingDocuments();
        var context = await CreateContextAsync(ct);
        return Ok(context.Documents.AsNoTracking());
    }

    /// <summary>
    /// Returns a single document by key.
    /// OData route: GET /odata/Documents({key})
    /// </summary>
    /// <param name="key">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxExpansionDepth = 3, MaxNodeCount = 200)]
    [ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]     
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == key, ct);

        if (document is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return NotFound();
        }

        LoggingService.RetrievingDocument(key, document.LineOfDutyCaseId);      
        return Ok(document);
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
            .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId, d.Content })
            .FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return NotFound();
        }

        if (doc.Content is null || doc.Content.Length == 0)
        {
            LoggingService.DocumentContentNotFound(key);
            return NotFound();
        }

        LoggingService.DownloadingDocument(key, doc.LineOfDutyCaseId);

        Response.Headers.ContentDisposition = $"attachment; filename=\"{doc.FileName}\"";
        return File(doc.Content, doc.ContentType, doc.FileName);
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
        [FromForm] string documentType = "Supporting Document",
        [FromForm] string description = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Count == 0)
        {
            LoggingService.InvalidUpload(caseId);
            return BadRequest("No file provided.");
        }

        foreach (var f in file)
        {
            var extension = Path.GetExtension(f.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                LoggingService.InvalidUpload(caseId);
                return BadRequest($"File type '{extension}' is not permitted.");
            }

            if (f.Length > MaxDocumentSize)
            {
                LoggingService.InvalidUpload(caseId);
                return BadRequest($"File '{f.FileName}' exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
            }

            if (FileSignatures.TryGetValue(extension, out var signatures))
            {
                using var headerStream = f.OpenReadStream();
                var headerBytes = new byte[8];
                var bytesRead = await headerStream.ReadAsync(headerBytes.AsMemory(0, headerBytes.Length), ct);

                if (!signatures.Any(sig => sig.Length <= bytesRead && headerBytes.AsSpan(0, sig.Length).SequenceEqual(sig)))
                {
                    LoggingService.InvalidUpload(caseId);
                    return BadRequest($"File '{f.FileName}' content does not match the '{extension}' file type.");
                }
            }
        }

        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var documents = new List<LineOfDutyDocument>(file.Count);

        foreach (var f in file)
        {
            LoggingService.UploadingDocument(caseId);
            using var ms = new MemoryStream();
            await using var stream = f.OpenReadStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            var document = new LineOfDutyDocument
            {
                LineOfDutyCaseId = caseId,
                FileName = f.FileName,
                ContentType = f.ContentType,
                DocumentType = documentType,
                Description = description,
                Content = bytes,
                FileSize = bytes.Length,
                UploadDate = DateTime.UtcNow
            };

            context.Documents.Add(document);
            documents.Add(document);
        }

        await context.SaveChangesAsync(ct);

        foreach (var document in documents)
        {
            document.Content = null!;
            LoggingService.DocumentUploaded(document.Id, caseId);
        }

        return Ok(documents);
    }

    /// <summary>
    /// Deletes a document by its identifier.
    /// Standard OData route: DELETE /odata/Documents({key})
    /// </summary>
    public async Task<IActionResult> Delete([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await ContextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents.FindAsync(new object[] { key }, ct);

        if (document is null)
        {
            LoggingService.DocumentNotFound(key, 0);
            return NotFound();
        }

        LoggingService.DeletingDocument(key, document.LineOfDutyCaseId);
        context.Documents.Remove(document);
        await context.SaveChangesAsync(ct);
        LoggingService.DocumentDeleted(key, document.LineOfDutyCaseId);
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
            return NotFound();

        var pdfBytes = _pdfService.GenerateFilledForm(lodCase);
        return File(pdfBytes, "application/pdf", $"AF348_{lodCase.CaseId}.pdf");
}
}
