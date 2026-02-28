using ECTSystem.Shared.Enums;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    internal record WorkflowTransition(
        WorkflowState TargetState,
        string ConfirmMessage,
        string ConfirmTitle,
        string OkButtonText,
        string BusyMessage,
        NotificationSeverity Severity,
        string NotifySummary,
        string NotifyDetail);

    /// <summary>
    /// Workflow transitions shared across all source states (return-* and board-* actions).
    /// </summary>
    private static readonly Dictionary<string, WorkflowTransition> SharedTransitions = new()
    {
        ["return-med-tech"] = new(
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Medical Technician...",
            NotificationSeverity.Info, "Returned to Medical Technician",
            "Case has been returned to the Medical Technician for review."),

        ["return-med-officer"] = new(
            WorkflowState.MedicalOfficerReview,
            "Are you sure you want to return this case to the Medical Officer?",
            "Confirm Return", "Return",
            "Returning to Medical Officer...",
            NotificationSeverity.Info, "Returned to Medical Officer",
            "Case has been returned to the Medical Officer for review."),

        ["return-unit-cc"] = new(
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),

        ["return-wing-ja"] = new(
            WorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to return this case to the Wing Judge Advocate?",
            "Confirm Return", "Return",
            "Returning to Wing JA...",
            NotificationSeverity.Info, "Returned to Wing JA",
            "Case has been returned to the Wing Judge Advocate for review."),

        ["return-wing-cc"] = new(
            WorkflowState.WingCommanderReview,
            "Are you sure you want to return this case to the Wing Commander?",
            "Confirm Return", "Return",
            "Returning to Wing CC...",
            NotificationSeverity.Info, "Returned to Wing CC",
            "Case has been returned to the Wing Commander for review."),

        ["return-appointing-authority"] = new(
            WorkflowState.AppointingAuthorityReview,
            "Are you sure you want to return this case to the Appointing Authority?",
            "Confirm Return", "Return",
            "Returning to Appointing Authority...",
            NotificationSeverity.Info, "Returned to Appointing Authority",
            "Case has been returned to the Appointing Authority for review."),

        ["board-tech"] = new(
            WorkflowState.BoardMedicalTechnicianReview,
            "Are you sure you want to forward this case to the Board Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Technician...",
            NotificationSeverity.Success, "Forwarded to Board Technician",
            "Case has been forwarded to the Board Technician."),

        ["board-med"] = new(
            WorkflowState.BoardMedicalOfficerReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        ["board-legal"] = new(
            WorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        ["board-admin"] = new(
            WorkflowState.BoardAdministratorReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),
    };

    /// <summary>
    /// Workflow transitions that depend on the source state: default forwards and context-dependent actions.
    /// </summary>
    private static readonly Dictionary<(WorkflowState Source, string Action), WorkflowTransition> SourceTransitions = new()
    {
        // Default forward transitions (main button click, item?.Value is null)
        [(WorkflowState.MemberInformationEntry, "default")] = new(
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to forward this case to the Medical Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Technician...",
            NotificationSeverity.Success, "Forwarded to Medical Technician",
            "Case has been forwarded to the Medical Technician for review."),

        [(WorkflowState.MedicalTechnicianReview, "default")] = new(
            WorkflowState.MedicalOfficerReview,
            "Are you sure you want to forward this case to the Medical Officer?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Officer...",
            NotificationSeverity.Success, "Forwarded to Medical Officer",
            "Case has been forwarded to the Medical Officer for review."),

        [(WorkflowState.MedicalOfficerReview, "default")] = new(
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to forward this case to the Unit Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Unit CC...",
            NotificationSeverity.Success, "Forwarded to Unit CC",
            "Case has been forwarded to the Unit Commander."),

        [(WorkflowState.UnitCommanderReview, "default")] = new(
            WorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to forward this case to the Wing Judge Advocate?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing JA...",
            NotificationSeverity.Success, "Forwarded to Wing JA",
            "Case has been forwarded to the Wing Judge Advocate."),

        [(WorkflowState.WingJudgeAdvocateReview, "default")] = new(
            WorkflowState.WingCommanderReview,
            "Are you sure you want to forward this case to the Wing Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing CC...",
            NotificationSeverity.Success, "Forwarded to Wing CC",
            "Case has been forwarded to the Wing Commander."),

        [(WorkflowState.WingCommanderReview, "default")] = new(
            WorkflowState.AppointingAuthorityReview,
            "Are you sure you want to forward this case to the Appointing Authority?",
            "Confirm Forward", "Forward",
            "Forwarding to Appointing Authority...",
            NotificationSeverity.Success, "Forwarded to Appointing Authority",
            "Case has been forwarded to the Appointing Authority for review."),

        [(WorkflowState.AppointingAuthorityReview, "default")] = new(
            WorkflowState.BoardMedicalTechnicianReview,
            "Are you sure you want to forward this case to the Board for review?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Review...",
            NotificationSeverity.Success, "Forwarded to Board Review",
            "Case has been forwarded to the Board for review."),

        [(WorkflowState.BoardMedicalTechnicianReview, "default")] = new(
            WorkflowState.BoardMedicalOfficerReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        [(WorkflowState.BoardMedicalOfficerReview, "default")] = new(
            WorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        [(WorkflowState.BoardLegalReview, "default")] = new(
            WorkflowState.BoardAdministratorReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),

        [(WorkflowState.BoardAdministratorReview, "default")] = new(
            WorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        [(WorkflowState.Completed, "default")] = new(
            WorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        // Context-dependent "return" action (target varies by source state)
        [(WorkflowState.MedicalOfficerReview, "return")] = new(
            WorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Med Tech...",
            NotificationSeverity.Info, "Returned to Med Tech",
            "Case has been returned to the Medical Technician for review."),

        [(WorkflowState.WingCommanderReview, "return")] = new(
            WorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),
    };
}
