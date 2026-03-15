using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Query;
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
public class DocumentsController : ODataControllerBase
{
    public DocumentsController(IDbContextFactory<EctDbContext> contextFactory, ILoggingService loggingService)
        : base(contextFactory, loggingService)
    {
    }

    /// <summary>
    /// Returns an IQueryable of documents for OData query composition.
    /// OData route: GET /odata/Documents
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    [EnableQuery(MaxTop = 100, PageSize = 50, MaxExpansionDepth = 3, MaxNodeCount = 200)]
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
}
