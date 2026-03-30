using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// Client-side service interface for LOD case document operations.
/// Provides methods for querying, uploading, deleting, and generating documents
/// associated with a Line of Duty case. Maps to <c>DocumentsController</c> (OData navigation
/// property queries) and <c>DocumentFilesController</c> (file upload/download via REST).
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Fetches all documents for the given case as a flat list, excluding binary file content.
    /// Returns document metadata only (type, filename, size, dates, etc.).
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A list of <see cref="LineOfDutyDocument"/> metadata entries for the case, or an empty list if none exist.</returns>
    Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queries documents for a case via OData with filtering, paging, sorting, and count.
    /// Returns a paged result set suitable for binding to <c>RadzenDataGrid</c>.
    /// Excludes binary file content; returns metadata only.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case.</param>
    /// <param name="filter">An OData <c>$filter</c> expression to restrict results, or <c>null</c> for no filtering.</param>
    /// <param name="top">The maximum number of documents to return (<c>$top</c>), or <c>null</c> for the server default.</param>
    /// <param name="skip">The number of documents to skip for paging (<c>$skip</c>), or <c>null</c> for no offset.</param>
    /// <param name="orderby">An OData <c>$orderby</c> expression (e.g., <c>"UploadDate desc"</c>), or <c>null</c> for default ordering.</param>
    /// <param name="count">If <c>true</c>, requests an inline count of total matching documents for paging UI.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>An <see cref="ODataServiceResult{T}"/> containing the matching documents and optional total count.</returns>
    Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads a document file to the given case via multipart form POST to the
    /// <c>DocumentFilesController</c>. The file is stored with a default document type
    /// of "Supporting Document".
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case to attach the document to.</param>
    /// <param name="fileName">The original filename of the uploaded file (e.g., <c>"medical-report.pdf"</c>).</param>
    /// <param name="contentType">The MIME content type of the file (e.g., <c>"application/pdf"</c>).</param>
    /// <param name="content">The raw byte array of the file content.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The created <see cref="LineOfDutyDocument"/> entity as returned by the server, including the server-assigned ID and upload timestamp.</returns>
    Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document from the given case via the <c>DocumentFilesController</c> REST endpoint.
    /// Removes both the metadata record and the stored file content.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case owning the document.</param>
    /// <param name="documentId">The database primary key of the document to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads the filled AF Form 348 PDF for the specified case.
    /// The server populates the official form template with current case data
    /// and returns the resulting PDF as a byte array.
    /// </summary>
    /// <param name="caseId">The database primary key of the LOD case to generate the form for.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A byte array containing the populated AF Form 348 PDF file content.</returns>
    Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default);
}
