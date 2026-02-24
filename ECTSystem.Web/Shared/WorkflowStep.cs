using ECTSystem.Shared.Enums;

namespace ECTSystem.Web.Shared;

public class WorkflowStep
{
    public int Number { get; set; }
    public LineOfDutyWorkflowState WorkflowState { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public WorkflowStepStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string CompletionDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public string CompletedBy { get; set; } = string.Empty;

    public int? DaysInProcess
    {
        get
        {
            if (StartDate is null) return null;
            var end = EndDate ?? DateTime.Now;
            return (int)(end.Date - StartDate.Value.Date).TotalDays;
        }
    }
}
