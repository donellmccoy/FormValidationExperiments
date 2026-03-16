using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class DocumentHttpService : ODataServiceBase, IDocumentService
{
    public DocumentHttpService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

    public async Task<List<LineOfDutyDocument>> GetDocumentsAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        const string select = "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion";
        var response = await HttpClient.GetFromJsonAsync<ODataResponse<LineOfDutyDocument>>(
            $"odata/Cases({caseId})/Documents?$select={select}", JsonOptions, cancellationToken);

        return response?.Value ?? [];
    }

    public async Task<ODataServiceResult<LineOfDutyDocument>> GetDocumentsAsync(
        int caseId, string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        const string select = "Id,LineOfDutyCaseId,DocumentType,FileName,ContentType,FileSize,UploadDate,Description,CreatedBy,CreatedDate,ModifiedBy,ModifiedDate,RowVersion";
        var url = BuildNavigationPropertyUrl($"odata/Cases({caseId})/Documents", filter, top, skip, orderby, count, select);
        var response = await HttpClient.GetFromJsonAsync<ODataCountResponse<LineOfDutyDocument>>(url, JsonOptions, cancellationToken);

        return new ODataServiceResult<LineOfDutyDocument>
        {
            Value = response?.Value?.ToList() ?? [],
            Count = response?.Count ?? 0
        };
    }

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

        return (await response.Content.ReadFromJsonAsync<LineOfDutyDocument>(JsonOptions, cancellationToken))!;
    }

    public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegative(documentId);

        var response = await HttpClient.DeleteAsync($"api/cases/{caseId}/documents/{documentId}", cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        return await HttpClient.GetByteArrayAsync($"api/cases/{caseId}/form348", cancellationToken);
    }
}
