using ECTSystem.Shared.Models;
using ECTSystem.Shared.ViewModels;

namespace ECTSystem.Shared.Mapping;

/// <summary>
/// Maps <see cref="CreateWorkflowStateHistoryDto"/> to <see cref="WorkflowStateHistory"/>.
/// </summary>
public static class WorkflowStateHistoryDtoMapper
{
    public static WorkflowStateHistory ToEntity(CreateWorkflowStateHistoryDto dto)
    {
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
            WorkflowState = dto.WorkflowState,
            Action = dto.Action,
            Status = dto.Status,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            SignedDate = dto.SignedDate,
            SignedBy = dto.SignedBy,
            PerformedBy = dto.PerformedBy,
        };
    }
}
