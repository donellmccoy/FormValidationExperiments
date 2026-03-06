namespace ECTSystem.Shared.Enums;

public enum LineOfDutyTrigger
{
    None,
    StartLineOfDutyCase,
    ForwardToMemberInformationEntry,
    ForwardToMedicalTechnician,
    ForwardToMedicalOfficerReview,
    ForwardToUnitCommanderReview,
    ForwardToWingJudgeAdvocateReview,
    ForwardToWingCommanderReview,
    ForwardToAppointingAuthorityReview,
    ForwardToApprovingAuthorityReview,
    ForwardToBoardTechnicianReview,
    ForwardToBoardMedicalReview,
    ForwardToBoardLegalReview,
    ForwardToBoardAdministratorReview,

    Return,
    Cancel,
    Complete,
    Close,
    Reopen
}