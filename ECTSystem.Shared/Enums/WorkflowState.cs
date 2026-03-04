namespace ECTSystem.Shared.Enums;

/// <summary>
/// Represents the current workflow state of a Line of Duty case as it progresses
/// through the review and determination process per DAFI 36-2910.
/// </summary>
public enum WorkflowState
{
    Pending = 0,

    /// <summary>Initial state — member information and incident details are being entered.</summary>
    MemberInformationEntry = 1,

    /// <summary>Case is under review by the medical technician.</summary>
    MedicalTechnicianReview = 2,

    /// <summary>Case is under review by the medical officer.</summary>
    MedicalOfficerReview = 3,

    /// <summary>Case is under review by the unit commander for recommendation.</summary>
    UnitCommanderReview = 4,

    /// <summary>Case is under review by the Wing Judge Advocate for legal sufficiency.</summary>
    WingJudgeAdvocateReview = 5,

    /// <summary>Case is under review by the appointing authority for formal determination.</summary>
    AppointingAuthorityReview = 6,

    /// <summary>Case is under review by the wing commander.</summary>
    WingCommanderReview = 7,

    /// <summary>Case is under review by the board medical technician.</summary>
    BoardMedicalTechnicianReview = 8,

    /// <summary>Case is under review by the board medical officer.</summary>
    BoardMedicalOfficerReview = 9,

    /// <summary>Case is under review by board legal counsel.</summary>
    BoardLegalReview = 10,

    /// <summary>Case is under administrative review by the board for final package preparation.</summary>
    BoardAdministratorReview = 11,

    /// <summary>LOD determination has been finalized and the case is closed.</summary>
    Completed = 12,

    /// <summary>Case is closed without a formal LOD determination (e.g., withdrawn, duplicate).</summary>  
    Closed = 13,

    /// <summary>Case is withdrawn from processing while in an active workflow state.</summary>
    Cancelled = 14
}
