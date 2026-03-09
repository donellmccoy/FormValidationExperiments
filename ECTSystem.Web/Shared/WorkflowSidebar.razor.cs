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
    /// dates, and signatures; falls back to positional <see cref="TimelineStep"/> data
    /// for cases that predate the history feature.
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

        // Primary source: most recent history entry per WorkflowState (by CreatedDate)
        var historyByState = lodCase?.WorkflowStateHistories?
            .GroupBy(h => h.WorkflowState)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.CreatedDate).First())
            ?? [];

        // Fallback: positional TimelineStep data (backward compatibility with seeded cases)
        var timelineByIndex = lodCase?.TimelineSteps
            .Select((ts, i) => (Index: i + 1, Step: ts))
            .ToDictionary(x => x.Index, x => x.Step)
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

            if (historyByState.TryGetValue(step.WorkflowState, out var history))
            {
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
                // No history — fall back to positional timeline data
                var timeline = timelineByIndex.GetValueOrDefault(step.Number);

                if (step.Number < stateInt)
                {
                    step.Status = WorkflowStepStatus.Completed;
                    step.StatusText = "Completed";
                    if (string.IsNullOrEmpty(step.CompletionDate))
                    {
                        step.CompletionDate = timeline?.CompletionDate?.ToString("MM/dd/yyyy h:mm tt") ?? DateTime.Now.ToString("MM/dd/yyyy h:mm tt");
                    }
                }
                else if (step.Number == stateInt)
                {
                    step.Status = WorkflowStepStatus.InProgress;
                    step.StatusText = string.Empty;
                    step.CompletionDate = string.Empty;
                }

                step.StartDate = timeline?.StartDate;
                step.SignedDate = timeline?.SignedDate;
                step.SignedBy = timeline?.SignedBy ?? string.Empty;
                step.CompletedDate = timeline?.CompletionDate;
                step.CompletedBy = timeline?.ModifiedBy ?? string.Empty;
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
}
