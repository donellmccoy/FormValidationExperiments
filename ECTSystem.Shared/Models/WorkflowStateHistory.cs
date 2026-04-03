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

    public DateTime EnteredDate { get; set; }

    public DateTime? ExitDate { get; set; }
}
