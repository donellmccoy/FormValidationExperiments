using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty timeline operations.
/// </summary>
public interface ILineOfDutyTimelineService
{
    Task<List<TimelineStep>> GetTimelineStepsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<TimelineStep> AddTimelineStepAsync(TimelineStep step, CancellationToken ct = default);
    Task<TimelineStep> UpdateTimelineStepAsync(TimelineStep step, CancellationToken ct = default);
}
