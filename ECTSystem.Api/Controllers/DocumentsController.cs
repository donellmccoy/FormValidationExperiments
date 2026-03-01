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
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _log.QueryingDocuments();
        var context = await CreateContextAsync(ct);
        return Ok(context.Documents.AsNoTracking());
    }

    // GET /odata/Documents(1)
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
        var doc = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
            .Select(d => new { d.FileName, d.ContentType, d.LineOfDutyCaseId, d.Content })
            .FirstOrDefaultAsync(ct);

        if (doc is null)
        {
            _log.DocumentNotFound(key, 0);
            return NotFound();
        }

        if (doc.Content is null || doc.Content.Length == 0)
        {
            _log.DocumentContentNotFound(key);
            return NotFound();
        }

        _log.DownloadingDocument(key, doc.LineOfDutyCaseId);
        return File(doc.Content, doc.ContentType, doc.FileName);
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

        if (file.Length > MaxDocumentSize)
        {
            _log.InvalidUpload(caseId);
            return BadRequest($"File size exceeds the maximum allowed size of {MaxDocumentSize / (1024 * 1024)} MB.");
        }

        _log.UploadingDocument(caseId);
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
        _log.DocumentUploaded(document.Id, caseId);
        return Created(document);
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

    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }
}
