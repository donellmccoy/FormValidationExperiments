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

/// <summary>
/// OData-enabled controller for querying document metadata.
/// Binary content operations (upload, download, delete) are handled by <see cref="DocumentFilesController"/>.
/// Named "DocumentsController" to match the OData entity set "Documents" (convention routing).
/// </summary>
[Authorize]
public class DocumentsController : ODataController
{
    /// <summary>Factory for creating scoped <see cref="EctDbContext"/> instances per request.</summary>
    private readonly IDbContextFactory<EctDbContext> _contextFactory;

    /// <summary>Service used for structured logging.</summary>
    private readonly ILoggingService _loggingService;

    /// <summary>
    /// Initializes a new instance of <see cref="DocumentsController"/>.
    /// </summary>
    /// <param name="contextFactory">The EF Core context factory.</param>
    /// <param name="loggingService">The structured logging service.</param>
    public DocumentsController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
    {
        _contextFactory = contextFactory;
        _loggingService = loggingService;
    }

    /// <summary>
    /// Returns an IQueryable of documents for OData query composition.
    /// OData route: GET /odata/Documents
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50)]
    public async Task<IActionResult> Get(CancellationToken ct = default)
    {
        _loggingService.QueryingDocuments();
        var context = await CreateContextAsync(ct);
        return Ok(context.Documents
            .AsNoTracking()
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
                // Content intentionally omitted — use the download endpoint for file bytes
            }));
    }

    /// <summary>
    /// Returns a single document by key.
    /// OData route: GET /odata/Documents({key})
    /// </summary>
    /// <param name="key">The document identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<IActionResult> Get([FromODataUri] int key, CancellationToken ct = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var document = await context.Documents
            .AsNoTracking()
            .Where(d => d.Id == key)
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
                // Content intentionally omitted — use the download endpoint for file bytes
            })
            .FirstOrDefaultAsync(ct);

        if (document is null)
        {
            _loggingService.DocumentNotFound(key, 0);
            return NotFound();
        }

        _loggingService.RetrievingDocument(key, document.LineOfDutyCaseId);
        return Ok(document);
    }

    /// <summary>
    /// Creates a scoped <see cref="EctDbContext"/> and registers it for disposal at the end of the HTTP response.
    /// Use this helper when returning an <see cref="IQueryable"/> so the context remains alive during serialization.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="EctDbContext"/> registered for response-lifetime disposal.</returns>
    private async Task<EctDbContext> CreateContextAsync(CancellationToken ct = default)
    {
        var context = await _contextFactory.CreateDbContextAsync(ct);
        HttpContext.Response.RegisterForDispose(context);
        return context;
    }
}
