using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Extensions;

public static class LineOfDutyExtensions
{
    public static void UpdateWorkflowState(this LineOfDutyCase lineOfDutyCase, WorkflowState newWorkflowState)
    {
        if (lineOfDutyCase.WorkflowState != newWorkflowState)
        {
            lineOfDutyCase.WorkflowState = newWorkflowState;
            lineOfDutyCase.ModifiedDate = DateTime.UtcNow;
        }
    }
    public static void AddWorkflowStateHistory(this LineOfDutyCase lineOfDutyCase, WorkflowState workflowState)
    {
        lineOfDutyCase.WorkflowStateHistories.Add(new WorkflowStateHistory
        {
            LineOfDutyCaseId = lineOfDutyCase.Id,
            WorkflowState = workflowState,
            Action = TransitionAction.Enter,
            StartDate = lineOfDutyCase.CreatedDate,
            PerformedBy = lineOfDutyCase.CreatedBy,
            CreatedDate = lineOfDutyCase.CreatedDate,
            CreatedBy = lineOfDutyCase.CreatedBy,
            ModifiedDate = lineOfDutyCase.ModifiedDate,
            ModifiedBy = lineOfDutyCase.ModifiedBy  
        });
    }
}
