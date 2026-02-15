using FormValidationExperiments.Shared.Models;
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
        string? orderby = null, bool? count = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a single LOD case by CaseId with all navigation properties.
    /// </summary>
    Task<LineOfDutyCase?> GetCaseAsync(string caseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves (PUTs) a LOD case entity. Returns the saved entity.
    /// </summary>
    Task<LineOfDutyCase> SaveCaseAsync(LineOfDutyCase lodCase, CancellationToken cancellationToken = default);
}
