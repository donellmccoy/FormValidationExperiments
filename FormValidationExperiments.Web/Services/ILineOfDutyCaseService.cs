using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// Client-side service interface for Line of Duty API operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    /// <summary>
    /// Returns a paged result of LOD cases with optional filtering and sorting.
    /// </summary>
    Task<PagedResult<LineOfDutyCase>> GetCasesPagedAsync(int skip, int take, string? filter = null, string? orderBy = null);

    /// <summary>
    /// Returns all mapped view models for a specific case.
    /// </summary>
    Task<CaseViewModelsDto> GetCaseViewModelsAsync(string caseId);

    /// <summary>
    /// Saves all view model changes for a case. Returns the refreshed CaseInfoModel.
    /// </summary>
    Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto);
}
