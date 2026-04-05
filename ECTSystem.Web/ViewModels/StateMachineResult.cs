using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using Radzen;

namespace ECTSystem.Web.ViewModels;

/// <summary>
/// Encapsulates the outcome of a state machine operation (transition, save, sign, or create).
/// Provides the updated <see cref="LineOfDutyCase"/>, the computed tab index, and success/error
/// information so the UI layer can apply results without containing business logic.
/// </summary>
internal sealed class StateMachineResult
{
    public bool Success { get; init; }
    public LineOfDutyCase Case { get; init; }
    public int TabIndex { get; init; }
    public string ErrorMessage { get; init; }

    public static StateMachineResult Ok(LineOfDutyCase lodCase, int tabIndex)
        => new() { Success = true, Case = lodCase, TabIndex = tabIndex };

    public static StateMachineResult Fail(string error)
        => new() { Success = false, ErrorMessage = error };
}

/// <summary>
/// Immutable descriptor for a workflow state transition, combining the trigger/target
/// state information used by the state machine with the UI metadata (confirmation
/// messages, notification details) used by the page component.
/// </summary>
internal record WorkflowTransition(
    WorkflowTrigger Trigger,
    WorkflowState TargetState,
    string ConfirmMessage,
    string ConfirmTitle,
    string OkButtonText,
    string BusyMessage,
    NotificationSeverity Severity,
    string NotifySummary,
    string NotifyDetail);
