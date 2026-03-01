using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
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

    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of <see cref="DocumentFilesController"/>.
    /// </summary>
    /// <param name="contextFactory">The EF Core context factory.</param>
    /// <param name="loggingService">The structured logging service.</param>
    public DocumentFilesController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
    {
        _contextFactory = contextFactory;
        _loggingService = loggingService;
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
    /// Uploads a new document and associates it with the specified case.
    /// REST route: POST /api/cases/{caseId}/documents
    /// </summary>
    /// <param name="caseId">The LOD case identifier to associate the document with.</param>
    /// <param name="file">The multipart form file to upload.</param>
    /// <param name="documentType">The category/type label for the document.</param>
    /// <param name="description">Optional human-readable description of the document.</param>
    /// <param name="ct">Cancellation token.</param>
    [HttpPost("{caseId}/documents")]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Upload(
        [FromRoute] int caseId,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string description = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            _loggingService.InvalidUpload(caseId);
            return BadRequest("No file provided.");
        }

        if (string.IsNullOrWhiteSpace(documentType))
        {
            _loggingService.InvalidUpload(caseId);
            return BadRequest("documentType is required.");
        }

        if (file.Length > MaxDocumentSize)
        {
            _loggingService.InvalidUpload(caseId);
            return BadRequest($"File size exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
        }

        _loggingService.UploadingDocument(caseId);
        using var ms = new MemoryStream();
        await using var stream = file.OpenReadStream();
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        var document = new LineOfDutyDocument
        {
            LineOfDutyCaseId = caseId,
            FileName = file.FileName,
            ContentType = file.ContentType,
            DocumentType = documentType,
            Description = description,
            Content = bytes,
            FileSize = bytes.Length,
            UploadDate = DateTime.UtcNow
        };

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        context.Documents.Add(document);
        await context.SaveChangesAsync(ct);

        document.Content = null!;
        _loggingService.DocumentUploaded(document.Id, caseId);
        return CreatedAtAction(nameof(Download), new { caseId, key = document.Id }, document);
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
}
