using ECTSystem.Shared.Enums;

namespace ECTSystem.Web.Pages;

/// <summary>
/// Provides a mapping between workflow tab indices and their corresponding <see cref="WorkflowState"/> values
/// for the Line of Duty determination wizard. Determines which tab to display for a given state and whether
/// a tab should be disabled based on the current workflow position.
/// </summary>
public static class WorkflowTabHelper
{
    private static readonly (string TabName, WorkflowState State)[] WorkflowTabMap =
    [
        ("Member Information",       WorkflowState.MemberInformationEntry),
        ("Medical Technician",       WorkflowState.MedicalTechnicianReview),
        ("Medical Officer",          WorkflowState.MedicalOfficerReview),
        ("Unit CC Review",           WorkflowState.UnitCommanderReview),
        ("Wing JA Review",           WorkflowState.WingJudgeAdvocateReview),
        ("Appointing Authority",     WorkflowState.AppointingAuthorityReview),
        ("Wing CC Review",           WorkflowState.WingCommanderReview),
        ("Board Technician Review",  WorkflowState.BoardMedicalTechnicianReview),
        ("Board Medical Review",     WorkflowState.BoardMedicalOfficerReview),
        ("Board Legal Review",       WorkflowState.BoardLegalReview),
        ("Board Admin Review",       WorkflowState.BoardAdministratorReview),
    ];

    /// <summary>
    /// Returns the tab index that corresponds to the given <see cref="WorkflowState"/>.
    /// Terminal states (Completed, Cancelled) map to the last workflow tab; Draft maps to 0.
    /// </summary>
    /// <param name="state">The workflow state to resolve to a tab index.</param>
    /// <returns>The zero-based index of the tab that corresponds to <paramref name="state"/>.</returns>
    public static int GetTabIndexForState(WorkflowState state)
    {
        for (var i = 0; i < WorkflowTabMap.Length; i++)
        {
            if (WorkflowTabMap[i].State == state)
            {
                return i;
            }
        }

        return state switch
        {
            WorkflowState.Completed => WorkflowTabMap.Length - 1,
            WorkflowState.Draft => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Determines whether a tab at the given index should be disabled based on the current workflow state.
    /// Tabs beyond the workflow tab range (e.g., Documents, Timeline) are always enabled.
    /// </summary>
    /// <param name="tabIndex">The zero-based index of the tab to check.</param>
    /// <param name="currentState">The current <see cref="WorkflowState"/> of the case.</param>
    /// <returns>
    /// <c>true</c> if <paramref name="tabIndex"/> refers to a workflow tab that is ahead of the
    /// current workflow state; otherwise <c>false</c>.
    /// </returns>
    public static bool IsTabDisabled(int tabIndex, WorkflowState currentState)
    {
        return tabIndex < WorkflowTabMap.Length && tabIndex > GetTabIndexForState(currentState);
    }
}
