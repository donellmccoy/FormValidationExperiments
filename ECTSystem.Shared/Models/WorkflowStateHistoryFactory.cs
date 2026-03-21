using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

public static class WorkflowStateHistoryFactory
{
    public static WorkflowStateHistory CreateInitialHistory(int caseId, WorkflowState state, DateTime? startDate = null)
    {
        var now = startDate ?? DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Status = WorkflowStepStatus.InProgress,
            StartDate = now,
            CreatedDate = now,
            ModifiedDate = now
        };
    }

    public static WorkflowStateHistory CreateCompleted(
        int caseId,
        WorkflowState state,
        DateTime? stepStartDate)
    {
        var now = DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Status = WorkflowStepStatus.Completed,
            StartDate = stepStartDate,
            EndDate = now,
            CreatedDate = now,
            ModifiedDate = now
        };
    }

    public static WorkflowStateHistory CreateReturned(
        int caseId,
        WorkflowState state,
        DateTime? stepStartDate)
    {
        var now = DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Status = WorkflowStepStatus.Pending,
            StartDate = stepStartDate,
            CreatedDate = now,
            ModifiedDate = now
        };
    }
}
