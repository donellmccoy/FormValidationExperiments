using FormValidationExperiments.Shared.Models;
using FormValidationExperiments.Shared.ViewModels;
using Radzen;

#nullable enable

namespace FormValidationExperiments.Web.Services;

/// <summary>
/// Client-side service interface for Line of Duty API operations.
/// </summary>
public interface ILineOfDutyCaseService
{
    /// <summary>
    /// Queries LOD cases via OData with filtering, paging, sorting, and count.
    /// </summary>
    Task<ODataServiceResult<LineOfDutyCase>> GetCasesAsync(
        string? filter = null, int? top = null, int? skip = null,
        string? orderby = null, bool? count = null);

    /// <summary>
    /// Returns all mapped view models for a specific case.
    /// </summary>
    Task<CaseViewModelsDto> GetCaseViewModelsAsync(string caseId);

    /// <summary>
    /// Saves all view model changes for a case. Returns the refreshed CaseInfoModel.
    /// </summary>
    Task<CaseInfoModel> SaveCaseAsync(string caseId, CaseViewModelsDto dto);
}
