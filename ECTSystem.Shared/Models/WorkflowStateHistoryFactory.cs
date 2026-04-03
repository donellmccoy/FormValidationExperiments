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
            EnteredDate = now,
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
            EnteredDate = stepStartDate ?? now,
            ExitDate = now,
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
            EnteredDate = stepStartDate ?? now,
            CreatedDate = now,
            ModifiedDate = now
        };
    }
}
