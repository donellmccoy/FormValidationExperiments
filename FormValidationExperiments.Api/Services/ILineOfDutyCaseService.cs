using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

namespace FormValidationExperiments.Api.Services;

/// <summary>
/// Service interface for Line of Duty case operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    Task<PagedResult<LineOfDutyCase>> GetCasesPagedAsync(int skip, int take, string? filter = null, string? orderBy = null, CancellationToken ct = default);
    Task<LineOfDutyCase?> GetCaseByIdAsync(int id, CancellationToken ct = default);
    Task<LineOfDutyCase?> GetCaseByCaseIdAsync(string caseId, CancellationToken ct = default);
    Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default);
    Task<LineOfDutyCase> UpdateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default);
    Task<LineOfDutyCase?> UpdateCaseAsync(string caseId, Action<LineOfDutyCase> applyChanges, CancellationToken ct = default);
    Task<bool> DeleteCaseAsync(int id, CancellationToken ct = default);
}

/// <summary>
/// Service interface for Line of Duty document operations.
/// </summary>
public interface ILineOfDutyDocumentService
{
    Task<List<LineOfDutyDocument>> GetDocumentsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyDocument?> GetDocumentByIdAsync(int documentId, CancellationToken ct = default);
    Task<byte[]?> GetDocumentContentAsync(int documentId, CancellationToken ct = default);
    Task<LineOfDutyDocument> UploadDocumentAsync(int caseId, string fileName, string contentType, string documentType, string description, Stream content, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(int documentId, CancellationToken ct = default);
}

/// <summary>
/// Service interface for Line of Duty appeal operations.
/// </summary>
public interface ILineOfDutyAppealService
{
    Task<List<LineOfDutyAppeal>> GetAppealsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyAppeal> AddAppealAsync(LineOfDutyAppeal appeal, CancellationToken ct = default);
}

/// <summary>
/// Service interface for Line of Duty authority operations.
/// </summary>
public interface ILineOfDutyAuthorityService
{
    Task<List<LineOfDutyAuthority>> GetAuthoritiesByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyAuthority> AddAuthorityAsync(LineOfDutyAuthority authority, CancellationToken ct = default);
}

/// <summary>
/// Service interface for Line of Duty timeline operations.
/// </summary>
public interface ILineOfDutyTimelineService
{
    Task<List<TimelineStep>> GetTimelineStepsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<TimelineStep> AddTimelineStepAsync(TimelineStep step, CancellationToken ct = default);
    Task<TimelineStep> UpdateTimelineStepAsync(TimelineStep step, CancellationToken ct = default);
}
