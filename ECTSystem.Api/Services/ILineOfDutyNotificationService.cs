using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

/// <summary>
/// Service interface for Line of Duty notification operations.
/// </summary>
public interface ILineOfDutyNotificationService
{
    Task<List<Notification>> GetNotificationsByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<Notification> AddNotificationAsync(Notification notification, CancellationToken ct = default);
    Task<bool> MarkAsReadAsync(int notificationId, CancellationToken ct = default);
}
