using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty document operations.
/// </summary>
public interface ILineOfDutyDocumentService
{
    Task<List<LineOfDutyDocument>> GetDocumentsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyDocument> GetDocumentByIdAsync(int documentId, CancellationToken ct = default);
    Task<byte[]> GetDocumentContentAsync(int documentId, CancellationToken ct = default);
    Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, string documentType, string description, Stream content, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(int documentId, CancellationToken ct = default);
}
