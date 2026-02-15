using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty authority operations.
/// </summary>
public interface ILineOfDutyAuthorityService
{
    Task<List<LineOfDutyAuthority>> GetAuthoritiesByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<LineOfDutyAuthority> AddAuthorityAsync(LineOfDutyAuthority authority, CancellationToken ct = default);
}
