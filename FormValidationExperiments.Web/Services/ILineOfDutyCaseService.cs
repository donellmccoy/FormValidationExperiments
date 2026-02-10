using FormValidationExperiments.Web.Models;

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// Service interface for Line of Duty database operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    // Case operations
    Task<List<LineOfDutyCase>> GetAllCasesAsync();
    Task<LineOfDutyCase> GetCaseByIdAsync(int id);
    Task<LineOfDutyCase> GetCaseByCaseIdAsync(string caseId);
    Task<LineOfDutyCase> CreateCaseAsync(LineOfDutyCase lodCase);
    Task<LineOfDutyCase> UpdateCaseAsync(LineOfDutyCase lodCase);
    Task<bool> DeleteCaseAsync(int id);

    // Document operations
    Task<List<LineOfDutyDocument>> GetDocumentsByCaseIdAsync(int caseId);
    Task<LineOfDutyDocument> AddDocumentAsync(LineOfDutyDocument document);
    Task<bool> DeleteDocumentAsync(int documentId);

    // Appeal operations
    Task<List<LineOfDutyAppeal>> GetAppealsByCaseIdAsync(int caseId);
    Task<LineOfDutyAppeal> AddAppealAsync(LineOfDutyAppeal appeal);

    // Authority operations
    Task<List<LineOfDutyAuthority>> GetAuthoritiesByCaseIdAsync(int caseId);
    Task<LineOfDutyAuthority> AddAuthorityAsync(LineOfDutyAuthority authority);

    // Timeline operations
    Task<List<TimelineStep>> GetTimelineStepsByCaseIdAsync(int caseId);
    Task<TimelineStep> AddTimelineStepAsync(TimelineStep step);
    Task<TimelineStep> UpdateTimelineStepAsync(TimelineStep step);
}
