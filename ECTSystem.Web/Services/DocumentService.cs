using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData/HTTP client for the per-case document attachments collection.
/// </summary>
/// <remarks>
/// <para>
/// Mixes the OData client (for <c>$filter</c> + <c>$select</c> metadata reads) with raw
/// <see cref="HttpClient"/> calls for paths the OData client cannot serve: multipart uploads
/// (<see cref="UploadDocumentAsync"/>), binary stream downloads, and the rendered Form 348 PDF
/// (<see cref="GetForm348PdfAsync"/>). Reads always carry an explicit <c>$select</c> matching the
/// projection the UI consumes — never <c>SELECT *</c> — to keep payloads bounded.
/// </para>
/// <para>
/// <b>Authenticated downloads</b> — <see cref="GetDocumentDownloadUrl"/> returns the canonical
/// <c>odata/Documents({key})/$value</c> URL for callers that need a direct link. Because the
/// endpoint is <c>[Authorize]</c>-protected and the bearer token is not on the URL, raw
/// <c>&lt;a href&gt;</c> anchors will not work; the UI must download via this service's
/// <see cref="HttpClient"/> (which carries the token) and present the bytes as a blob URL.
/// Deferred follow-up: a server-side <c>Documents({key})/Default.GetSignedUrl</c> bound action
/// could return a short-lived, scoped URL for native browser downloads — never accept tokens via
/// query string.
/// </para>
/// <para>
/// <b>Upload defaults</b> — <see cref="UploadDocumentAsync"/> hard-codes <c>documentType</c> to
/// <c>"Miscellaneous"</c> and an empty <c>description</c>. Callers that need richer metadata must
/// follow up with a PATCH; this is intentional for the current single upload UI surface.
/// </para>
/// </remarks>
public class DocumentService : ODataServiceBase, IDocumentService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentService"/> class.
    /// </summary>
    /// <param name="context">The OData client context for query composition.</param>
    /// <param name="httpClient">The named <c>OData</c> <see cref="HttpClient"/> used for multipart uploads, deletes, and binary downloads.</param>
    /// <param name="logger">The logger for diagnostic events.</param>
    public DocumentService(
        EctODataContext context,
        HttpClient httpClient,
        ILogger<DocumentService> logger,
        [Microsoft.Extensions.DependencyInjection.FromKeyedServices(ECTSystem.Web.Extensions.ServiceCollectionExtensions.ODataJsonOptionsKey)] System.Text.Json.JsonSerializerOptions jsonOptions)
        : base(context, httpClient, logger, jsonOptions) { }

    public async Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var query = Context.Documents
            .AddQueryOption("$filter", $"LineOfDutyCaseId eq {caseId}")
            .AddQueryOption("$select", "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion");

        return await ExecuteQueryAsync(query, cancellationToken);
    }

    public async Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        var caseFilter = $"LineOfDutyCaseId eq {caseId}";
        var combinedFilter = string.IsNullOrEmpty(filter) ? caseFilter : $"({caseFilter}) and ({filter})";

        var query = Context.Documents
            .AddQueryOption("$filter", combinedFilter)
            .AddQueryOption("$select", "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion");

        if (top.HasValue)
            query = query.AddQueryOption("$top", top.Value);

        if (skip.HasValue)
            query = query.AddQueryOption("$skip", skip.Value);

        if (!string.IsNullOrEmpty(orderby))
            query = query.AddQueryOption("$orderby", orderby);

        if (count == true)
        {
            var (items, totalCount) = await ExecutePagedQueryAsync(query, cancellationToken);

            return new ODataServiceResult<LineOfDutyDocument>
            {
                Value = items,
                Count = totalCount
            };
        }

        var results = await ExecuteQueryAsync(query, cancellationToken);

        return new ODataServiceResult<LineOfDutyDocument>
        {
            Value = results,
            Count = results.Count
        };
    }

    // Multipart file upload — HttpClient required (OData client doesn't support multipart form data)
    public async Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, byte[] content, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(content);

        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);

        form.Add(fileContent, "file", fileName);
        form.Add(new StringContent("Miscellaneous"), "documentType");
        form.Add(new StringContent(string.Empty), "description");

        var response = await HttpClient.PostAsync($"odata/Cases({caseId})/Documents", form, cancellationToken);

        await EnsureSuccessOrThrowAsync(response, $"POST odata/Cases({caseId})/Documents", cancellationToken);

        return (await response.Content.ReadFromJsonAsync<LineOfDutyDocument>(JsonOptions, cancellationToken))!;
    }

    public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);

        var response = await HttpClient.DeleteAsync($"odata/Documents({documentId})", cancellationToken);
        await EnsureSuccessOrThrowAsync(response, $"DELETE odata/Documents({documentId})", cancellationToken);
    }

    // Binary stream download — HttpClient required (OData client doesn't support raw byte responses)
    public async Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        return await HttpClient.GetByteArrayAsync($"api/cases/{caseId}/form348", cancellationToken);
    }

    public string GetDocumentDownloadUrl(int documentId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);

        return $"{HttpClient.BaseAddress}odata/Documents({documentId})/$value";
    }
}
