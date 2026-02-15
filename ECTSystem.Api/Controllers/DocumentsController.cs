using Microsoft.AspNetCore.Mvc;
using ECTSystem.Api.Logging;
using ECTSystem.Api.Services;

namespace ECTSystem.Api.Controllers;

[ApiController]
[Route("api/cases/{caseId:int}/documents")]
public class DocumentsController : ControllerBase
{
    private readonly ILineOfDutyDocumentService _documentService;
    private readonly IApiLogService _log;

    public DocumentsController(ILineOfDutyDocumentService documentService, IApiLogService log)
    {
        _documentService = documentService;
        _log = log;
    }

    [HttpGet]
    public async Task<IActionResult> GetByCaseId(int caseId, CancellationToken ct = default)
    {
        _log.QueryingDocuments(caseId);
        var documents = await _documentService.GetDocumentsByCaseIdAsync(caseId, ct);
        return Ok(documents);
    }

    [HttpGet("{documentId:int}")]
    public async Task<IActionResult> GetById(int caseId, int documentId, CancellationToken ct = default)
    {
        _log.RetrievingDocument(documentId, caseId);
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
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
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
        if (document is null || document.LineOfDutyCaseId != caseId)
        {
            _log.DocumentNotFound(documentId, caseId);
            return NotFound();
        }

        var content = await _documentService.GetDocumentContentAsync(documentId, ct);
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
            await using var stream = file.OpenReadStream();
            var document = await _documentService.UploadDocumentAsync(
                caseId,
                file.FileName,
                file.ContentType,
                documentType,
                description,
                stream,
                ct);

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
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
        if (document is null || document.LineOfDutyCaseId != caseId)
        {
            _log.DocumentNotFound(documentId, caseId);
            return NotFound();
        }

        await _documentService.DeleteDocumentAsync(documentId, ct);
        _log.DocumentDeleted(documentId, caseId);
        return NoContent();
    }
}
