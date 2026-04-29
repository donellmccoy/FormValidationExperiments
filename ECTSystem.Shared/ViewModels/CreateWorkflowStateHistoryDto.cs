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

    // EnteredDate and ExitDate are intentionally omitted — these audit timestamps
    // are server-authoritative and stamped by WorkflowStateHistoryController via
    // TimeProvider. See §2.7 (N1) of the controller best-practices remediation.
}
