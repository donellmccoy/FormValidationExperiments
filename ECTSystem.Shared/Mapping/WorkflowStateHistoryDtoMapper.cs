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
        // EnteredDate / ExitDate are stamped server-side by the controller via
        // TimeProvider — see §2.7 (N1) of the controller best-practices remediation.
        return new WorkflowStateHistory
        {
            LineOfDutyCaseId = dto.LineOfDutyCaseId,
            WorkflowState = dto.WorkflowState,
        };
    }
}
