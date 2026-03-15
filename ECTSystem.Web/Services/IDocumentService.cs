using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD case document operations.
/// Maps to <c>DocumentsController</c> and <c>DocumentFilesController</c>.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Fetches all documents for the given case.
    /// </summary>
    Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries documents for a case via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a document file to the given case via the Documents API.
    /// </summary>
    Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the given case via the Documents API.
    /// </summary>
    Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the filled AF Form 348 PDF for the specified case.
    /// </summary>
    Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default);
}
