using ECTSystem.Shared.Models;

namespace ECTSystem.Api.Services;

public interface IWorkflowStepHistoryService
{
    Task<List<WorkflowStepHistory>> GetHistoryByCaseIdAsync(int caseId, CancellationToken ct = default);
    Task<WorkflowStepHistory> AddHistoryEntryAsync(WorkflowStepHistory entry, CancellationToken ct = default);
}
