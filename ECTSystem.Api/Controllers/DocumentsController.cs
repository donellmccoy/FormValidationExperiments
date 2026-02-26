using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/cases/{caseId:int}/documents")]
public class DocumentsController : ControllerBase
{
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10 MB

    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IApiLogService _log;

    public DocumentsController(IDbContextFactory<EctDbContext> contextFactory, IApiLogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> GetByCaseId(int caseId, CancellationToken ct = default)
    {
        _log.QueryingDocuments(caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var documents = await context.Documents
            .AsNoTracking()
            .Where(d => d.LineOfDutyCaseId == caseId)
            .Select(d => new LineOfDutyDocument
            {
                Id = d.Id,
                LineOfDutyCaseId = d.LineOfDutyCaseId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                UploadDate = d.UploadDate,
                Description = d.Description
                // Content intentionally excluded
            })
            .ToListAsync(ct);
        return Ok(documents);
    }

    [HttpGet("{documentId:int}")]
    public async Task<IActionResult> GetById(int caseId, int documentId, CancellationToken ct = default)
    {
        _log.RetrievingDocument(documentId, caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new LineOfDutyDocument
            {
                Id = d.Id,
                LineOfDutyCaseId = d.LineOfDutyCaseId,
                DocumentType = d.DocumentType,
                FileName = d.FileName,
                ContentType = d.ContentType,
                FileSize = d.FileSize,
                UploadDate = d.UploadDate,
                Description = d.Description
            })
            .FirstOrDefaultAsync(ct);

        if (document is null || document.LineOfDutyCaseId != caseId)
        {
            _log.DocumentNotFound(documentId, caseId);
            return NotFound();
        }

        return Ok(document);
    }

    [HttpGet("{documentId:int}/download")]
    public async Task<IActionResult> Download(int caseId, int documentId, CancellationToken ct = default)
    {
        _log.DownloadingDocument(documentId, caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => new LineOfDutyDocument
            {
                Id = d.Id,
                LineOfDutyCaseId = d.LineOfDutyCaseId,
                FileName = d.FileName,
                ContentType = d.ContentType
            })
            .FirstOrDefaultAsync(ct);

        if (document is null || document.LineOfDutyCaseId != caseId)
        {
            _log.DocumentNotFound(documentId, caseId);
            return NotFound();
        }

        var content = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == documentId)
            .Select(d => d.Content)
            .FirstOrDefaultAsync(ct);

        if (content is null)
        {
            _log.DocumentContentNotFound(documentId);
            return NotFound();
        }

        return File(content, document.ContentType, document.FileName);
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<IActionResult> Upload(
        int caseId,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string description = "",
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
        {
            _log.InvalidUpload(caseId);
            return BadRequest("No file provided.");
        }

        _log.UploadingDocument(caseId);
        try
        {
            using var ms = new MemoryStream();
            await using var stream = file.OpenReadStream();
            await stream.CopyToAsync(ms, ct);
            var bytes = ms.ToArray();

            if (bytes.Length > MaxDocumentSize)
            {
                throw new ArgumentException($"File size exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
            }

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

            document.Content = null!; // Don't return content in response
            _log.DocumentUploaded(document.Id, caseId);
            return CreatedAtAction(nameof(GetById), new { caseId, documentId = document.Id }, document);
        }
        catch (ArgumentException ex)
        {
            _log.UploadFailed(caseId, ex);
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{documentId:int}")]
    public async Task<IActionResult> Delete(int caseId, int documentId, CancellationToken ct = default)
    {
        _log.DeletingDocument(documentId, caseId);
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents.FindAsync([documentId], ct);

        if (document is null || document.LineOfDutyCaseId != caseId)
        {
            _log.DocumentNotFound(documentId, caseId);
            return NotFound();
        }

        context.Documents.Remove(document);
        await context.SaveChangesAsync(ct);
        _log.DocumentDeleted(documentId, caseId);
        return NoContent();
    }
}
