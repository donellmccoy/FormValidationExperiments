using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Snapshot history entry recording the state of a workflow step at the moment
/// a transition occurred. The latest entry per WorkflowState drives sidebar display.
/// </summary>
public class WorkflowStepHistory : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public LineOfDutyWorkflowState WorkflowState { get; set; }
    public TransitionAction Action { get; set; }
    public WorkflowStepStatus Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string PerformedBy { get; set; } = string.Empty;

    public LineOfDutyCase LineOfDutyCase { get; set; }
}
