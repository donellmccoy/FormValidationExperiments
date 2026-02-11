using Microsoft.AspNetCore.Mvc;
using FormValidationExperiments.Api.Services;

namespace FormValidationExperiments.Api.Controllers;

[ApiController]
[Route("api/cases/{caseId:int}/documents")]
public class DocumentsController : ControllerBase
{
    private readonly ILineOfDutyDocumentService _documentService;

    public DocumentsController(ILineOfDutyDocumentService documentService)
    {
        _documentService = documentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetByCaseId(int caseId, CancellationToken ct = default)
    {
        var documents = await _documentService.GetDocumentsByCaseIdAsync(caseId, ct);
        return Ok(documents);
    }

    [HttpGet("{documentId:int}")]
    public async Task<IActionResult> GetById(int caseId, int documentId, CancellationToken ct = default)
    {
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
        if (document is null || document.LineOfDutyCaseId != caseId)
            return NotFound();

        return Ok(document);
    }

    [HttpGet("{documentId:int}/download")]
    public async Task<IActionResult> Download(int caseId, int documentId, CancellationToken ct = default)
    {
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
        if (document is null || document.LineOfDutyCaseId != caseId)
            return NotFound();

        var content = await _documentService.GetDocumentContentAsync(documentId, ct);
        if (content is null)
            return NotFound();

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
            return BadRequest("No file provided.");

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

            return CreatedAtAction(nameof(GetById), new { caseId, documentId = document.Id }, document);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{documentId:int}")]
    public async Task<IActionResult> Delete(int caseId, int documentId, CancellationToken ct = default)
    {
        var document = await _documentService.GetDocumentByIdAsync(documentId, ct);
        if (document is null || document.LineOfDutyCaseId != caseId)
            return NotFound();

        await _documentService.DeleteDocumentAsync(documentId, ct);
        return NoContent();
    }
}
