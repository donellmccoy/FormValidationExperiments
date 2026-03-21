using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Snapshot history entry recording the state of a workflow step at the moment
/// a transition occurred. The latest entry per WorkflowState drives sidebar display.
/// </summary>
public class WorkflowStateHistory : AuditableEntity
{
    public int Id { get; set; }

    public int LineOfDutyCaseId { get; set; }

    public WorkflowState WorkflowState { get; set; }

    public WorkflowStepStatus Status { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }
}
