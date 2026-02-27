using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.EntityFrameworkCore;
using ECTSystem.Api.Logging;
using ECTSystem.Persistence.Data;
using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Controllers;

[Authorize]
public class DocumentsController : ODataController
{
    private const long MaxDocumentSize = 10 * 1024 * 1024; // 10 MB

    private readonly IDbContextFactory<EctDbContext> _contextFactory;
    private readonly IApiLogService _log;

    public DocumentsController(IDbContextFactory<EctDbContext> contextFactory, IApiLogService log)
    {
        _contextFactory = contextFactory;
        _log = log;
    }

    // GET /odata/Documents
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public IActionResult Get()
    {
        _log.QueryingDocuments();
        var context = _contextFactory.CreateDbContext();
        return Ok(context.Documents.AsNoTracking());
    }

    // GET /odata/Documents(1)
    [EnableQuery]
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == key, ct);

        if (document is null)
        {
            _log.DocumentNotFound(key, 0);
            return NotFound();
        }

        _log.RetrievingDocument(key, document.LineOfDutyCaseId);
        return Ok(document);
    }

    // GET /api/cases/{caseId}/documents/{key}/download — download binary content
    [HttpGet("api/cases/{caseId}/documents/{key}/download")]
    [HttpGet("odata/Documents({key})/$value")]
    public async Task<IActionResult> GetMediaResource([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var meta = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
            .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId })
            .FirstOrDefaultAsync(ct);

        if (meta is null)
        {
            _log.DocumentNotFound(key, 0);
            return NotFound();
        }

        _log.DownloadingDocument(key, meta.LineOfDutyCaseId);

        var content = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
            .Select(d => d.Content)
            .FirstOrDefaultAsync(ct);

        if (content is null || content.Length == 0)
        {
            _log.DocumentContentNotFound(key);
            return NotFound();
        }

        return File(content, meta.ContentType, meta.FileName);
    }

    // POST /api/cases/{caseId}/documents — multipart file upload
    [HttpPost("api/cases/{caseId}/documents")]
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

            document.Content = null!;
            _log.DocumentUploaded(document.Id, caseId);
            return Created(document);
        }
        catch (ArgumentException ex)
        {
            _log.UploadFailed(caseId, ex);
            return BadRequest(ex.Message);
        }
    }

    // DELETE /api/cases/{caseId}/documents/{key}
    [HttpDelete("api/cases/{caseId}/documents/{key}")]
    public async Task<IActionResult> Delete([FromRoute] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents.FindAsync([key], ct);

        if (document is null)
        {
            _log.DocumentNotFound(key, 0);
            return NotFound();
        }

        _log.DeletingDocument(key, document.LineOfDutyCaseId);
        context.Documents.Remove(document);
        await context.SaveChangesAsync(ct);
        _log.DocumentDeleted(key, document.LineOfDutyCaseId);
        return NoContent();
    }
}
