using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using PanoramicData.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

/// <summary>
/// OData HTTP service for LOD case document operations.
/// Implements <see cref="IDocumentService"/> by combining two API surfaces:
/// <list type="bullet">
///   <item><description>OData navigation property queries via <c>Cases({id})/Documents</c> for metadata retrieval.</description></item>
///   <item><description>REST endpoints on <c>DocumentFilesController</c> (<c>api/cases/{id}/documents</c>) for file upload, download, and deletion.</description></item>
/// </list>
/// Document queries explicitly select metadata columns and exclude binary <c>FileContent</c> to minimize payload size.
/// </summary>
public class DocumentHttpService : ODataServiceBase, IDocumentService
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentHttpService"/> class.
    /// </summary>
    /// <param name="client">The typed OData client for navigation property queries against <c>Cases({id})/Documents</c>.</param>
    /// <param name="httpClient">The raw HTTP client for REST file operations via <c>DocumentFilesController</c>.</param>
    public DocumentHttpService(ODataClient client, HttpClient httpClient)
        : base(client, httpClient) { }

    /// <inheritdoc />
    public async Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        const string select = "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion";
        var response = await HttpClient.GetFromJsonAsync<ODataResponse<LineOfDutyDocument>>(
            $"odata/Cases({caseId})/Documents?$select={select}", ODataJsonOptions, cancellationToken);

        return response?.Value ?? [];
    }

    /// <inheritdoc />
    public async Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        const string select = "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion";
        var url = BuildNavigationPropertyUrl($"odata/Cases({caseId})/Documents", filter, top, skip, orderby, count, select);
        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<LineOfDutyDocument>>(url, ODataJsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyDocument>
        {
            Value = response?.Value?.ToList() ?? [],
            Count = response?.Count ?? 0
        };
    }

    /// <inheritdoc />
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
        form.Add(new StringContent("Supporting Document"), "documentType");
        form.Add(new StringContent(string.Empty), "description");

        var response = await HttpClient.PostAsync($"api/cases/{caseId}/documents", form, cancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LineOfDutyDocument>(ODataJsonOptions, cancellationToken))!;
    }

    /// <inheritdoc />
    public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegative(documentId);

        var response = await HttpClient.DeleteAsync($"api/cases/{caseId}/documents/{documentId}", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        return await HttpClient.GetByteArrayAsync($"api/cases/{caseId}/form348", cancellationToken);
    }
}
