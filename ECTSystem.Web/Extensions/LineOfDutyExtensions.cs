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
}
