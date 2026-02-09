using Microsoft.AspNetCore.Components;

namespace FormValidationExperiments.Web.Shared;

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
        if (Steps.Count <= 1) return 0;
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
    Pending
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
}
