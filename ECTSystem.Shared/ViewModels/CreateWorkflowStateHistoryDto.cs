using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class CreateWorkflowStateHistoryDto
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "LineOfDutyCaseId is required.")]
    public int LineOfDutyCaseId { get; set; }

    [Required]
    public WorkflowState WorkflowState { get; set; }

    [Required]
    public TransitionAction Action { get; set; }

    [Required]
    public WorkflowStepStatus Status { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public string PerformedBy { get; set; } = string.Empty;
}
