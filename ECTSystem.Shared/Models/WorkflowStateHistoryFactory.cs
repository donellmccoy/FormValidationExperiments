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
            Action = TransitionAction.Entered,
            Status = WorkflowStepStatus.InProgress,
            StartDate = now,
            OccurredAt = now,
            PerformedBy = string.Empty,
            CreatedDate = now,
            ModifiedDate = now
        };
    }

    public static WorkflowStateHistory CreateSigned(
        int caseId,
        WorkflowState state,
        DateTime? stepStartDate,
        DateTime? signedDate,
        string signedBy)
    {
        var now = DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Action = TransitionAction.Signed,
            Status = WorkflowStepStatus.InProgress,
            StartDate = stepStartDate,
            SignedDate = signedDate,
            SignedBy = signedBy,
            OccurredAt = now,
            PerformedBy = string.Empty,
            CreatedDate = now,
            ModifiedDate = now
        };
    }

    public static WorkflowStateHistory CreateCompleted(
        int caseId,
        WorkflowState state,
        DateTime? stepStartDate,
        DateTime? signedDate = null,
        string signedBy = "")
    {
        var now = DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Action = TransitionAction.Completed,
            Status = WorkflowStepStatus.Completed,
            StartDate = stepStartDate,
            SignedDate = signedDate ?? now,
            SignedBy = signedBy ?? string.Empty,
            OccurredAt = now,
            PerformedBy = string.Empty,
            CreatedDate = now,
            ModifiedDate = now
        };
    }

    public static WorkflowStateHistory CreateReturned(
        int caseId,
        WorkflowState state,
        DateTime? stepStartDate,
        DateTime? signedDate = null,
        string signedBy = "")
    {
        var now = DateTime.UtcNow;
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = caseId,
            WorkflowState = state,
            Action = TransitionAction.Returned,
            Status = WorkflowStepStatus.Pending,
            StartDate = stepStartDate,
            SignedDate = signedDate,
            SignedBy = signedBy ?? string.Empty,
            OccurredAt = now,
            PerformedBy = string.Empty,
            CreatedDate = now,
            ModifiedDate = now
        };
    }
}
