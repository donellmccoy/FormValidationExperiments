using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

/// <summary>
/// REST controller for document file upload, download, and deletion.
/// Handles binary content operations that are not suited for OData.
/// Base route: <c>api/cases</c>.
/// </summary>
[Authorize]
[ApiController]
[Route("api/cases")]
public class DocumentFilesController : ControllerBase
{
    /// <summary>Maximum permitted document upload size (10 MB).</summary>
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10 MB

    /// <summary>Permitted file extensions for document uploads.</summary>
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx",
        ".jpg", ".jpeg", ".png", ".gif", ".tif", ".tiff",
        ".txt", ".rtf"
    };

    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>Service for generating filled AF Form 348 PDFs.</summary>
    private readonly AF348PdfService _pdfService;

    /// <summary>
    /// Initializes a new instance of <see cref="DocumentFilesController"/>.
    /// </summary>
    /// <param name="contextFactory">The EF Core context factory.</param>
    /// <param name="loggingService">The structured logging service.</param>
    /// <param name="pdfService">The AF Form 348 PDF generation service.</param>
    public DocumentFilesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService, AF348PdfService pdfService)
    {
        _contextFactory = contextFactory;
        _loggingService = loggingService;
        _pdfService = pdfService;
    }

    /// <summary>
    /// Downloads the binary content of a document as a file attachment.
    /// REST route: GET /api/cases/{caseId}/documents/{key}/download
    /// </summary>
    /// <param name="caseId">The LOD case identifier (used for logging and scoping).</param>
    /// <param name="key">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{caseId}/documents/{key}/download")]
    public async Task<IActionResult> Download([FromRoute] int caseId, [FromRoute] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var doc = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
            .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId, d.Content })
            .FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            _loggingService.DocumentNotFound(key, caseId);
            return NotFound();
        }

        if (doc.LineOfDutyCaseId != caseId)
        {
            _loggingService.DocumentNotFound(key, caseId);
            return NotFound();
        }

        if (doc.Content is null || doc.Content.Length == 0)
        {
            _loggingService.DocumentContentNotFound(key);
            return NotFound();
        }

        _loggingService.DownloadingDocument(key, doc.LineOfDutyCaseId);
        return File(doc.Content, doc.ContentType, doc.FileName);
    }

    /// <summary>
    /// Uploads one or more documents and associates them with the specified case.
    /// REST route: POST /api/cases/{caseId}/documents
    /// </summary>
    /// <param name="caseId">The LOD case identifier to associate the documents with.</param>
    /// <param name="file">The multipart form file(s) to upload.</param>
    /// <param name="documentType">The category/type label for the documents.</param>
    /// <param name="description">Optional human-readable description of the documents.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{caseId}/documents")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB total for multiple files
    public async Task<IActionResult> Upload(
        [FromRoute] int caseId,
        List<IFormFile> file,
        [FromForm] string documentType = "Supporting Document",
        [FromForm] string description = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Count == 0)
        {
            _loggingService.InvalidUpload(caseId);
            return BadRequest("No file provided.");
        }

        foreach (var f in file)
        {
            var extension = Path.GetExtension(f.FileName);
            if (!AllowedExtensions.Contains(extension))
            {
                _loggingService.InvalidUpload(caseId);
                return BadRequest($"File type '{extension}' is not permitted.");
            }

            if (f.Length > MaxDocumentSize)
            {
                _loggingService.InvalidUpload(caseId);
                return BadRequest($"File '{f.FileName}' exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
            }
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var documents = new List<LineOfDutyDocument>(file.Count);

        foreach (var f in file)
        {
            _loggingService.UploadingDocument(caseId);
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
            _loggingService.DocumentUploaded(document.Id, caseId);
        }

        return Ok(documents);
    }

    /// <summary>
    /// Deletes a document by its identifier.
    /// REST route: DELETE /api/cases/{caseId}/documents/{key}
    /// </summary>
    /// <param name="caseId">The LOD case identifier (used for logging).</param>
    /// <param name="key">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpDelete("{caseId}/documents/{key}")]
    public async Task<IActionResult> Delete([FromRoute] int caseId, [FromRoute] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents.FindAsync([key], ct);

        if (document is null)
        {
            _loggingService.DocumentNotFound(key, caseId);
            return NotFound();
        }

        if (document.LineOfDutyCaseId != caseId)
        {
            _loggingService.DocumentNotFound(key, caseId);
            return NotFound();
        }

        _loggingService.DeletingDocument(key, document.LineOfDutyCaseId);
        context.Documents.Remove(document);
        await context.SaveChangesAsync(ct);
        _loggingService.DocumentDeleted(key, document.LineOfDutyCaseId);
        return NoContent();
    }

    /// <summary>
    /// Generates a filled AF Form 348 PDF for the specified case.
    /// REST route: GET /api/cases/{caseId}/form348
    /// </summary>
    /// <param name="caseId">The LOD case identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpGet("{caseId:int}/form348")]
    public async Task<IActionResult> GetForm348([FromRoute] int caseId, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var lodCase = await context.Cases.AsNoTracking()
            .Include(c => c.Member)
            .FirstOrDefaultAsync(c => c.Id == caseId, ct);

        if (lodCase is null)
            return NotFound();

        var pdfBytes = _pdfService.GenerateFilledForm(lodCase);
        return File(pdfBytes, "application/pdf", $"AF348_{lodCase.CaseId}.pdf");
    }
}
