namespace ECTSystem.Shared.Enums;

public enum LodTrigger
{
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

    ReturnToUnitCommanderReview,
    ReturnToMedicalTechnicianReview,
    ReturnToMedicalOfficerReview,
    ReturnToWingJudgeAdvocateReview,
    ReturnToWingCommanderReview,
    ReturnToAppointingAuthorityReview,
    ReturnToApprovingAuthorityReview,

    Cancel,
    Complete,
    Close,
    Reopen
}
