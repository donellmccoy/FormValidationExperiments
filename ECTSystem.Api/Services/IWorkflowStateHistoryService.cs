using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

public interface IWorkflowStateHistoryService
{
    Task<List<WorkflowStateHistory>> GetHistoryByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<WorkflowStateHistory> AddHistoryEntryAsync(WorkflowStateHistory entry, CancellationToken ct = default);
}
