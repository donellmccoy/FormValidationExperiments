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
    public DateTime EnteredDate { get; set; }
    public DateTime? ExitDate { get; set; }
}
