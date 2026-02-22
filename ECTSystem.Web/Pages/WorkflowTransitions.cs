using ECTSystem.Shared.Enums;
using Radzen;

namespace ECTSystem.Web.Pages;

public partial class EditCase
{
    internal record WorkflowTransition(
        LineOfDutyWorkflowState TargetState,
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
            LineOfDutyWorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Medical Technician...",
            NotificationSeverity.Info, "Returned to Medical Technician",
            "Case has been returned to the Medical Technician for review."),

        ["return-med-officer"] = new(
            LineOfDutyWorkflowState.MedicalOfficerReview,
            "Are you sure you want to return this case to the Medical Officer?",
            "Confirm Return", "Return",
            "Returning to Medical Officer...",
            NotificationSeverity.Info, "Returned to Medical Officer",
            "Case has been returned to the Medical Officer for review."),

        ["return-unit-cc"] = new(
            LineOfDutyWorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),

        ["return-wing-ja"] = new(
            LineOfDutyWorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to return this case to the Wing Judge Advocate?",
            "Confirm Return", "Return",
            "Returning to Wing JA...",
            NotificationSeverity.Info, "Returned to Wing JA",
            "Case has been returned to the Wing Judge Advocate for review."),

        ["return-wing-cc"] = new(
            LineOfDutyWorkflowState.WingCommanderReview,
            "Are you sure you want to return this case to the Wing Commander?",
            "Confirm Return", "Return",
            "Returning to Wing CC...",
            NotificationSeverity.Info, "Returned to Wing CC",
            "Case has been returned to the Wing Commander for review."),

        ["return-appointing-authority"] = new(
            LineOfDutyWorkflowState.AppointingAuthorityReview,
            "Are you sure you want to return this case to the Appointing Authority?",
            "Confirm Return", "Return",
            "Returning to Appointing Authority...",
            NotificationSeverity.Info, "Returned to Appointing Authority",
            "Case has been returned to the Appointing Authority for review."),

        ["board-tech"] = new(
            LineOfDutyWorkflowState.BoardTechnicianReview,
            "Are you sure you want to forward this case to the Board Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Technician...",
            NotificationSeverity.Success, "Forwarded to Board Technician",
            "Case has been forwarded to the Board Technician."),

        ["board-med"] = new(
            LineOfDutyWorkflowState.BoardMedicalReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        ["board-legal"] = new(
            LineOfDutyWorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        ["board-admin"] = new(
            LineOfDutyWorkflowState.BoardAdminReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),
    };

    /// <summary>
    /// Workflow transitions that depend on the source state: default forwards and context-dependent actions.
    /// </summary>
    private static readonly Dictionary<(LineOfDutyWorkflowState Source, string Action), WorkflowTransition> SourceTransitions = new()
    {
        // Default forward transitions (main button click, item?.Value is null)
        [(LineOfDutyWorkflowState.MemberInformationEntry, "default")] = new(
            LineOfDutyWorkflowState.MedicalTechnicianReview,
            "Are you sure you want to forward this case to the Medical Technician?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Technician...",
            NotificationSeverity.Success, "Forwarded to Medical Technician",
            "Case has been forwarded to the Medical Technician for review."),

        [(LineOfDutyWorkflowState.MedicalTechnicianReview, "default")] = new(
            LineOfDutyWorkflowState.MedicalOfficerReview,
            "Are you sure you want to forward this case to the Medical Officer?",
            "Confirm Forward", "Forward",
            "Forwarding to Medical Officer...",
            NotificationSeverity.Success, "Forwarded to Medical Officer",
            "Case has been forwarded to the Medical Officer for review."),

        [(LineOfDutyWorkflowState.MedicalOfficerReview, "default")] = new(
            LineOfDutyWorkflowState.UnitCommanderReview,
            "Are you sure you want to forward this case to the Unit Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Unit CC...",
            NotificationSeverity.Success, "Forwarded to Unit CC",
            "Case has been forwarded to the Unit Commander."),

        [(LineOfDutyWorkflowState.UnitCommanderReview, "default")] = new(
            LineOfDutyWorkflowState.WingJudgeAdvocateReview,
            "Are you sure you want to forward this case to the Wing Judge Advocate?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing JA...",
            NotificationSeverity.Success, "Forwarded to Wing JA",
            "Case has been forwarded to the Wing Judge Advocate."),

        [(LineOfDutyWorkflowState.WingJudgeAdvocateReview, "default")] = new(
            LineOfDutyWorkflowState.WingCommanderReview,
            "Are you sure you want to forward this case to the Wing Commander?",
            "Confirm Forward", "Forward",
            "Forwarding to Wing CC...",
            NotificationSeverity.Success, "Forwarded to Wing CC",
            "Case has been forwarded to the Wing Commander."),

        [(LineOfDutyWorkflowState.WingCommanderReview, "default")] = new(
            LineOfDutyWorkflowState.AppointingAuthorityReview,
            "Are you sure you want to forward this case to the Appointing Authority?",
            "Confirm Forward", "Forward",
            "Forwarding to Appointing Authority...",
            NotificationSeverity.Success, "Forwarded to Appointing Authority",
            "Case has been forwarded to the Appointing Authority for review."),

        [(LineOfDutyWorkflowState.AppointingAuthorityReview, "default")] = new(
            LineOfDutyWorkflowState.BoardTechnicianReview,
            "Are you sure you want to forward this case to the Board for review?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Review...",
            NotificationSeverity.Success, "Forwarded to Board Review",
            "Case has been forwarded to the Board for review."),

        [(LineOfDutyWorkflowState.BoardTechnicianReview, "default")] = new(
            LineOfDutyWorkflowState.BoardMedicalReview,
            "Are you sure you want to forward this case to the Board Medical reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Medical...",
            NotificationSeverity.Success, "Forwarded to Board Medical",
            "Case has been forwarded to the Board Medical reviewer."),

        [(LineOfDutyWorkflowState.BoardMedicalReview, "default")] = new(
            LineOfDutyWorkflowState.BoardLegalReview,
            "Are you sure you want to forward this case to the Board Legal reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Legal...",
            NotificationSeverity.Success, "Forwarded to Board Legal",
            "Case has been forwarded to the Board Legal reviewer."),

        [(LineOfDutyWorkflowState.BoardLegalReview, "default")] = new(
            LineOfDutyWorkflowState.BoardAdminReview,
            "Are you sure you want to forward this case to the Board Admin reviewer?",
            "Confirm Forward", "Forward",
            "Forwarding to Board Admin...",
            NotificationSeverity.Success, "Forwarded to Board Admin",
            "Case has been forwarded to the Board Admin reviewer."),

        [(LineOfDutyWorkflowState.BoardAdminReview, "default")] = new(
            LineOfDutyWorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        [(LineOfDutyWorkflowState.Completed, "default")] = new(
            LineOfDutyWorkflowState.Completed,
            "Are you sure you want to complete the Board review?",
            "Confirm Complete", "Complete",
            "Completing Board review...",
            NotificationSeverity.Success, "Review Completed",
            "The Board review has been completed."),

        // Context-dependent "return" action (target varies by source state)
        [(LineOfDutyWorkflowState.MedicalOfficerReview, "return")] = new(
            LineOfDutyWorkflowState.MedicalTechnicianReview,
            "Are you sure you want to return this case to the Medical Technician?",
            "Confirm Return", "Return",
            "Returning to Med Tech...",
            NotificationSeverity.Info, "Returned to Med Tech",
            "Case has been returned to the Medical Technician for review."),

        [(LineOfDutyWorkflowState.WingCommanderReview, "return")] = new(
            LineOfDutyWorkflowState.UnitCommanderReview,
            "Are you sure you want to return this case to the Unit Commander?",
            "Confirm Return", "Return",
            "Returning to Unit CC...",
            NotificationSeverity.Info, "Returned to Unit CC",
            "Case has been returned to the Unit Commander for review."),
    };
}
