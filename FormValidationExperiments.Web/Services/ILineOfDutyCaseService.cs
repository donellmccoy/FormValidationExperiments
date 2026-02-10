using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// Client-side service interface for Line of Duty API operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    /// <summary>
    /// Returns all LOD cases (lightweight, no navigation properties).
    /// </summary>
    Task<List<LineOfDutyCase>> GetAllCasesAsync();

    /// <summary>
    /// Returns all mapped view models for a specific case.
    /// </summary>
    Task<CaseViewModelsDto> GetCaseViewModelsAsync(string caseId);

    /// <summary>
    /// Saves all view model changes for a case. Returns the refreshed CaseInfoModel.
    /// </summary>
    Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto);
}
