using Microsoft.AspNetCore.Components;

namespace ECTSystem.Web.Shared;

public partial class WorkflowSidebar : ComponentBase
{
    [Parameter]
    public List<WorkflowStep> Steps { get; set; } = new();

    [Parameter]
    public int CurrentStepIndex { get; set; }

    [Parameter]
    public EventCallback<WorkflowStep> OnStepClicked { get; set; }

    private int GetProgressPercent()
    {
        if (Steps.Count <= 1)
        {
            return 0;
        }

        return (int)((double)CurrentStepIndex / (Steps.Count - 1) * 100);
    }

    private static string GetStepCssClass(WorkflowStep step)
    {
        return step.Status switch
        {
            WorkflowStepStatus.Completed => "completed",
            WorkflowStepStatus.InProgress => "active",
            WorkflowStepStatus.Pending => "pending",
            _ => ""
        };
    }
}

public enum WorkflowStepStatus
{
    Completed,
    InProgress,
    Pending,
    Cancelled,
    OnHold,
    Closed
}

public class WorkflowStep
{
    public int Number { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public WorkflowStepStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string CompletionDate { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CompletedDate { get; set; }
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
