using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty case CRUD operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    IQueryable<LineOfDutyCase> GetCasesQueryable();
    Task<LineOfDutyCase> GetCaseByKeyAsync(int key, CancellationToken ct = default);
    Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase, CancellationToken ct = default);
    Task<LineOfDutyCase> UpdateCaseAsync(int key, LineOfDutyCase update, CancellationToken ct = default);
    Task<bool> DeleteCaseAsync(int key, CancellationToken ct = default);
}

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

/// <summary>
/// Service interface for Line of Duty notification operations.
/// </summary>
public interface ILineOfDutyNotificationService
{
    Task<List<Notification>> GetNotificationsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<Notification> AddNotificationAsync(Notification notification, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(int notificationId, CancellationToken ct = default);
}
