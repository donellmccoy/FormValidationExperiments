using Microsoft.AspNetCore.Components;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.Web.Shared;

/// <summary>
/// Vertical step-progress sidebar component that displays workflow steps for an LOD case.
/// Each step corresponds to a <see cref="WorkflowState"/> in the determination workflow.
/// Provides visual indicators for completed, in-progress, and pending steps, along with
/// a progress bar showing overall case advancement.
/// </summary>
public partial class WorkflowSidebar : ComponentBase
{
    /// <summary>
    /// The LOD case whose workflow state and history determine step statuses.
    /// May be <c>null</c> for new cases (defaults to step 1).
    /// </summary>
    [Parameter]
    public LineOfDutyCase LineOfDutyCase { get; set; }

    /// <summary>
    /// Callback invoked when the user clicks a workflow step in the sidebar.
    /// The clicked <see cref="WorkflowStep"/> is passed as the argument, allowing the parent
    /// component to navigate to the corresponding tab or form section.
    /// </summary>
    [Parameter]
    public EventCallback<WorkflowStep> OnStepClicked { get; set; }

    /// <summary>
    /// The ordered list of workflow steps displayed in the sidebar.
    /// Each step represents a stage in the LOD determination workflow.
    /// </summary>
    public List<WorkflowStep> Steps { get; } =
    [
        new() { Number = 1,  Name = "Enter Member Information",  Icon = "flag",                 Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MemberInformationEntry,    Description = "Enter member identification and incident details to initiate the LOD case." },
        new() { Number = 2,  Name = "Medical Technician Review", Icon = "person",               Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MedicalTechnicianReview,   Description = "Medical technician reviews the injury/illness and documents clinical findings." },
        new() { Number = 3,  Name = "Medical Officer Review",    Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.MedicalOfficerReview,      Description = "Medical officer reviews the technician's findings and provides a clinical assessment." },
        new() { Number = 4,  Name = "Unit CC Review",            Icon = "edit_document",        Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.UnitCommanderReview,       Description = "Unit commander reviews the case and submits a recommendation for the LOD determination." },
        new() { Number = 5,  Name = "Wing JA Review",            Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.WingJudgeAdvocateReview,   Description = "Wing Judge Advocate reviews the case for legal sufficiency and compliance." },
        new() { Number = 6,  Name = "Appointing Authority",      Icon = "verified_user",        Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.AppointingAuthorityReview, Description = "Appointing authority reviews the case and issues a formal LOD determination." },
        new() { Number = 7,  Name = "Wing CC Review",            Icon = "stars",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.WingCommanderReview,       Description = "Wing commander reviews the case and renders a preliminary LOD determination." },
        new() { Number = 8,  Name = "Board Technician Review",   Icon = "rate_review",          Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardMedicalTechnicianReview,     Description = "Board medical technician reviews the case file for completeness and accuracy." },
        new() { Number = 9,  Name = "Board Medical Review",      Icon = "medical_services",     Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardMedicalOfficerReview,        Description = "Board medical officer reviews all medical evidence and provides a formal assessment." },
        new() { Number = 10, Name = "Board Legal Review",        Icon = "gavel",                Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardLegalReview,          Description = "Board legal counsel reviews the case for legal sufficiency before final decision." },
        new() { Number = 11, Name = "Board Admin Review",        Icon = "admin_panel_settings", Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.BoardAdministratorReview,          Description = "Board administrative officer finalizes the case package and prepares the formal determination." },
        new() { Number = 12, Name = "Completed",                 Icon = "check_circle",         Status = WorkflowStepStatus.Pending, WorkflowState = WorkflowState.Completed,                Description = "LOD determination has been finalized and the case is closed." }
    ];

    public static List<TimelineStep> TimelineSteps { get; } =
    [
        new() { StepDescription = "Enter Member Information",  WorkflowState = WorkflowState.MemberInformationEntry,         TimelineDays = 3,  IsOptional = false },
        new() { StepDescription = "Medical Technician Review", WorkflowState = WorkflowState.MedicalTechnicianReview,        TimelineDays = 5,  IsOptional = false },
        new() { StepDescription = "Medical Officer Review",    WorkflowState = WorkflowState.MedicalOfficerReview,           TimelineDays = 5,  IsOptional = false },
        new() { StepDescription = "Unit Commander Review",     WorkflowState = WorkflowState.UnitCommanderReview,            TimelineDays = 14, IsOptional = false },
        new() { StepDescription = "Wing JA Review",            WorkflowState = WorkflowState.WingJudgeAdvocateReview,        TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Appointing Authority",      WorkflowState = WorkflowState.AppointingAuthorityReview,      TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Wing CC Review",            WorkflowState = WorkflowState.WingCommanderReview,            TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Board Technician Review",   WorkflowState = WorkflowState.BoardMedicalTechnicianReview,   TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Board Medical Review",      WorkflowState = WorkflowState.BoardMedicalOfficerReview,      TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Board Legal Review",        WorkflowState = WorkflowState.BoardLegalReview,               TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Board Admin Review",        WorkflowState = WorkflowState.BoardAdministratorReview,       TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Completed",                 WorkflowState = WorkflowState.Completed,                     TimelineDays = 0,  IsOptional = false },
    ];


    /// <summary>
    /// The 0-based index of the currently active workflow step.
    /// </summary>
    public int CurrentStepIndex { get; private set; }

    /// <summary>
    /// The currently active <see cref="WorkflowStep"/>, or <c>null</c> if no steps are available.
    /// </summary>
    public WorkflowStep CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count ? Steps[CurrentStepIndex] : null;

    protected override void OnParametersSet()
    {
        ApplyWorkflowState();
    }

    /// <summary>
    /// Synchronizes step statuses and computes the current step index
    /// from the specified LOD case (or the bound <see cref="LineOfDutyCase"/> parameter).
    /// Uses <see cref="WorkflowStateHistory"/> entries as the primary source for step status,
    /// dates, and signatures.
    /// </summary>
    /// <param name="lodCase">
    /// Optional LOD case override. When <c>null</c>, uses the bound <see cref="LineOfDutyCase"/> parameter.
    /// </param>
    public void ApplyWorkflowState(LineOfDutyCase lodCase = null)
    {
        lodCase ??= LineOfDutyCase;

        // Clamp to valid range — DB rows that predate the WorkflowState migration have int value 0
        var rawState = lodCase is not null ? (int)lodCase.WorkflowState : 1;
        var stateInt = rawState < 1 ? 1 : rawState > Steps.Count ? Steps.Count : rawState;

        // Primary source: all history entries for each WorkflowState.
        // Grouped into lists so we can pick the semantically correct entry based
        // on the step's position relative to the current state.
        var historiesByState = lodCase?.WorkflowStateHistories?
            .GroupBy(h => h.WorkflowState)
            .ToDictionary(g => g.Key, g => g.ToList())
            ?? [];

        foreach (var step in Steps)
        {
            // Steps after the current step are always Pending with no data,
            // even if history entries exist from a previous forward pass before a return.
            if (step.Number > stateInt)
            {
                ClearStep(step);
                continue;
            }

            if (historiesByState.TryGetValue(step.WorkflowState, out var histories))
            {
                // For past steps, prefer the Completed entry; for the current step, prefer InProgress.
                // Always pick the most recent entry with the preferred status (highest Id)
                // because a step may have multiple entries after return transitions.
                var preferredStatus = step.Number < stateInt
                    ? WorkflowStepStatus.Completed
                    : WorkflowStepStatus.InProgress;

                var history = histories
                    .Where(h => h.Status == preferredStatus)
                    .OrderByDescending(h => h.Id)
                    .FirstOrDefault()
                    ?? histories.OrderByDescending(h => h.CreatedDate).ThenByDescending(h => h.Id).First();

                step.Status = history.Status;

                if (history.Status == WorkflowStepStatus.Completed)
                {
                    step.StartDate = history.StartDate;
                    step.EndDate = history.EndDate;
                    step.CompletedDate = history.EndDate;
                    step.SignedDate = history.SignedDate;
                    step.SignedBy = history.SignedBy ?? string.Empty;
                    step.CompletedBy = history.PerformedBy;
                    step.StatusText = "Completed";
                    step.CompletionDate = history.EndDate?.ToString("MM/dd/yyyy h:mm tt") ?? string.Empty;
                }
                else if (history.Status == WorkflowStepStatus.InProgress)
                {
                    step.StartDate = history.StartDate;
                    step.EndDate = null;
                    step.CompletedDate = null;
                    step.SignedDate = null;
                    step.SignedBy = string.Empty;
                    step.CompletedBy = string.Empty;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }
                else
                {
                    // Pending / returned — show nothing
                    step.StartDate = null;
                    step.EndDate = null;
                    step.CompletedDate = null;
                    step.SignedDate = null;
                    step.SignedBy = string.Empty;
                    step.CompletedBy = string.Empty;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }
            }
            else
            {
                // No history — set status based on position relative to current state
                if (step.Number < stateInt)
                {
                    step.Status = WorkflowStepStatus.Completed;
                    step.StatusText = "Completed";
                    if (string.IsNullOrEmpty(step.CompletionDate))
                    {
                        step.CompletionDate = DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                    }
                }
                else if (step.Number == stateInt)
                {
                    step.Status = WorkflowStepStatus.InProgress;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }
            }
        }

        CurrentStepIndex = stateInt - 1;
    }

    /// <summary>
    /// Resets a workflow step to the default Pending state with no data.
    /// Used for steps beyond the current workflow position.
    /// </summary>
    private static void ClearStep(WorkflowStep step)
    {
        step.Status = WorkflowStepStatus.Pending;
        step.StartDate = null;
        step.EndDate = null;
        step.CompletedDate = null;
        step.SignedDate = null;
        step.SignedBy = string.Empty;
        step.CompletedBy = string.Empty;
        step.StatusText = string.Empty;
        step.CompletionDate = string.Empty;
    }

    /// <summary>
    /// Calculates the progress bar percentage based on <see cref="CurrentStepIndex"/> relative
    /// to the total number of <see cref="Steps"/>. Returns 0 when there is one or fewer steps;
    /// otherwise returns a value from 0 to 100.
    /// </summary>
    /// <returns>An integer percentage (0–100) representing overall workflow progress.</returns>
    private int GetProgressPercent()
    {
        if (Steps.Count <= 1)
        {
            return 0;
        }

        return (int)((double)CurrentStepIndex / (Steps.Count - 1) * 100);
    }

    /// <summary>
    /// Returns the CSS class name for a workflow step based on its current
    /// <see cref="WorkflowStep.Status"/>: <c>"completed"</c>, <c>"active"</c>,
    /// <c>"pending"</c>, or an empty string for unrecognized statuses.
    /// </summary>
    /// <param name="step">The workflow step to evaluate.</param>
    /// <returns>A CSS class string used for styling the step in the sidebar.</returns>
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

    private static string FormatDaysInProcess(WorkflowStep step)
    {
        if (!step.DaysInProcess.HasValue)
        {
            return string.Empty;
        }

        var days = step.DaysInProcess.Value;
        var timelineStep = TimelineSteps.Find(t => t.WorkflowState == step.WorkflowState);

        if (timelineStep is null || timelineStep.TimelineDays <= 0)
        {
            return $"Days in Process \u2014 {days} days";
        }

        if (days > timelineStep.TimelineDays)
        {
            var overdue = days - timelineStep.TimelineDays;
            return $"Days in Process \u2014 {days} days | {overdue} Overdue";
        }

        if (days == timelineStep.TimelineDays)
        {
            return $"Days in Process \u2014 {days} days | Due today";
        }

        return $"Days in Process \u2014 {days} days";
    }
}
