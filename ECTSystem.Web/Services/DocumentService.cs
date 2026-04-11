using System.Net.Http.Json;
using ECTSystem.Shared.Models;
using Microsoft.OData.Client;
using Radzen;

#nullable enable

namespace ECTSystem.Web.Services;

public class DocumentService : ODataServiceBase, IDocumentService
{
    public DocumentService(EctODataContext context, HttpClient httpClient)
        : base(context, httpClient) { }

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
        form.Add(new StringContent("Supporting Document"), "documentType");
        form.Add(new StringContent(string.Empty), "description");

        var response = await HttpClient.PostAsync($"odata/Cases({caseId})/Documents", form, cancellationToken);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<LineOfDutyDocument>(JsonOptions, cancellationToken))!;
    }

    public async Task DeleteDocumentAsync(int caseId, int documentId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(documentId);

        var documentUri = new Uri(Context.BaseUri, $"Documents({documentId})");
        var trackedDocument = Context.Entities.FirstOrDefault(e => e.Identity == documentUri)?.Entity as LineOfDutyDocument;

        if (trackedDocument != null)
        {
            Context.DeleteObject(trackedDocument);
        }
        else
        {
            // Attach a stub entity to the context for deletion if not already tracked.
            // This avoids a GET request to fetch the entity before deleting it.
            var documentToDelete = new LineOfDutyDocument { Id = documentId };
            Context.AttachTo("Documents", documentToDelete);
            Context.DeleteObject(documentToDelete);
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    // Binary stream download — HttpClient required (OData client doesn't support raw byte responses)
    public async Task<byte[]> GetForm348PdfAsync(int caseId, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(caseId);

        return await HttpClient.GetByteArrayAsync($"odata/Cases({caseId})/Form348", cancellationToken);
    }
}
