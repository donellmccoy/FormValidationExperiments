using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty appeal operations.
/// </summary>
public interface ILineOfDutyAppealService
{
    Task<List<LineOfDutyAppeal>> GetAppealsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyAppeal> AddAppealAsync(LineOfDutyAppeal appeal, CancellationToken ct = default);
}
