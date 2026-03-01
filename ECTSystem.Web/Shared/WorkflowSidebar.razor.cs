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
    /// The ordered list of <see cref="WorkflowStep"/> instances to display in the sidebar.
    /// Each step represents a stage in the LOD determination workflow (e.g., Member Information,
    /// Medical Technician Review, Board Admin Review, Completed).
    /// </summary>
    [Parameter]
    public List<WorkflowStep> Steps { get; set; } = new();

    /// <summary>
    /// The 0-based index of the currently active workflow step within <see cref="Steps"/>.
    /// Used to calculate the progress bar percentage and highlight the active step.
    /// </summary>
    [Parameter]
    public int CurrentStepIndex { get; set; }

    /// <summary>
    /// Callback invoked when the user clicks a workflow step in the sidebar.
    /// The clicked <see cref="WorkflowStep"/> is passed as the argument, allowing the parent
    /// component to navigate to the corresponding tab or form section.
    /// </summary>
    [Parameter]
    public EventCallback<WorkflowStep> OnStepClicked { get; set; }

    /// <summary>
    /// Synchronizes the workflow sidebar step statuses and computes the current step index
    /// to match <paramref name="state"/>. Uses <see cref="WorkflowStateHistory"/> entries
    /// as the primary source for step status, dates, and signatures; falls back to positional
    /// <see cref="TimelineStep"/> data for cases that predate the history feature.
    /// </summary>
    /// <param name="steps">The list of workflow steps to update in place.</param>
    /// <param name="state">The target <see cref="WorkflowState"/> to synchronize to.</param>
    /// <param name="lodCase">
    /// The LOD case whose <see cref="LineOfDutyCase.WorkflowStateHistories"/> and
    /// <see cref="LineOfDutyCase.TimelineSteps"/> are used to populate step metadata.
    /// May be <c>null</c> for new cases.
    /// </param>
    /// <returns>The 0-based index of the current step corresponding to <paramref name="state"/>.</returns>
    public static int ApplyWorkflowState(List<WorkflowStep> steps, WorkflowState state, LineOfDutyCase lodCase)
    {
        // Clamp to valid range — DB rows that predate the WorkflowState migration have int value 0
        var stateInt = (int)state < 1 ? 1 : (int)state > steps.Count ? steps.Count : (int)state;

        // Primary source: latest history entry per WorkflowState (highest Id = most recent)
        var historyByState = lodCase?.WorkflowStateHistories?
            .GroupBy(h => h.WorkflowState)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => h.Id).First())
            ?? [];

        // Fallback: positional TimelineStep data (backward compatibility with seeded cases)
        var timelineByIndex = lodCase?.TimelineSteps
            .Select((ts, i) => (Index: i + 1, Step: ts))
            .ToDictionary(x => x.Index, x => x.Step)
            ?? [];

        foreach (var step in steps)
        {
            if (historyByState.TryGetValue(step.WorkflowState, out var history))
            {
                step.Status = history.Status;
                step.StartDate = history.StartDate;
                step.SignedDate = history.SignedDate;
                step.SignedBy = history.SignedBy ?? string.Empty;
                step.CompletedBy = history.PerformedBy;
                step.StatusText = history.Status == WorkflowStepStatus.Completed ? "Completed" : string.Empty;
                step.CompletedDate = history.Status == WorkflowStepStatus.Completed ? history.OccurredAt : null;
                step.CompletionDate = history.Status == WorkflowStepStatus.Completed ? history.OccurredAt.ToString("MM/dd/yyyy h:mm tt") : string.Empty;
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
                else
                {
                    step.Status = WorkflowStepStatus.Pending;
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

        return stateInt - 1;
    }

    /// <summary>
    /// Creates the full list of 12 workflow steps (Member Information through Completed),
    /// each initialized to <see cref="WorkflowStepStatus.Pending"/>, then applies
    /// <see cref="ApplyWorkflowState"/> to synchronize step statuses with the case's
    /// current <see cref="LineOfDutyCase.WorkflowState"/> and history data.
    /// </summary>
    /// <param name="lodCase">
    /// The LOD case whose workflow state and history determine initial step statuses.
    /// May be <c>null</c> for new cases (defaults to <see cref="WorkflowState.MemberInformationEntry"/>).
    /// </param>
    /// <returns>
    /// A tuple containing the initialized <see cref="WorkflowStep"/> list and the 0-based
    /// index of the current step.
    /// </returns>
    public static (List<WorkflowStep> Steps, int CurrentStepIndex) InitializeSteps(LineOfDutyCase lodCase)
    {
        var steps = new List<WorkflowStep>
        {
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
        };

        var currentStepIndex = ApplyWorkflowState(steps, lodCase?.WorkflowState ?? WorkflowState.MemberInformationEntry, lodCase);
        return (steps, currentStepIndex);
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
