using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Class representing a timeline step in the LOD process.
/// </summary>
public class TimelineStep : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public LineOfDutyCase LineOfDutyCase { get; set; }
    public string StepDescription { get; set; } = string.Empty; // e.g., "Member Reports", "Medical Provider Review"
    public int TimelineDays { get; set; } // e.g., 5 calendar days
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public WorkflowState? WorkflowState { get; set; }
    public int? ResponsibleAuthorityId { get; set; }
    public LineOfDutyAuthority ResponsibleAuthority { get; set; }

    /// <summary>
    /// Creates the default set of 12 timeline steps matching the LOD workflow states.
    /// </summary>
    public static List<TimelineStep> CreateDefaultSteps() =>
    [
        new() { StepDescription = "Enter Member Information",  WorkflowState = Enums.WorkflowState.MemberInformationEntry,         TimelineDays = 3,  IsOptional = false },
        new() { StepDescription = "Medical Technician Review", WorkflowState = Enums.WorkflowState.MedicalTechnicianReview,        TimelineDays = 5,  IsOptional = false },
        new() { StepDescription = "Medical Officer Review",    WorkflowState = Enums.WorkflowState.MedicalOfficerReview,           TimelineDays = 5,  IsOptional = false },
        new() { StepDescription = "Unit Commander Review",     WorkflowState = Enums.WorkflowState.UnitCommanderReview,            TimelineDays = 14, IsOptional = false },
        new() { StepDescription = "Wing JA Review",            WorkflowState = Enums.WorkflowState.WingJudgeAdvocateReview,        TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Appointing Authority",      WorkflowState = Enums.WorkflowState.AppointingAuthorityReview,      TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Wing CC Review",            WorkflowState = Enums.WorkflowState.WingCommanderReview,            TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Board Technician Review",   WorkflowState = Enums.WorkflowState.BoardMedicalTechnicianReview,   TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Board Medical Review",      WorkflowState = Enums.WorkflowState.BoardMedicalOfficerReview,      TimelineDays = 10, IsOptional = false },
        new() { StepDescription = "Board Legal Review",        WorkflowState = Enums.WorkflowState.BoardLegalReview,               TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Board Admin Review",        WorkflowState = Enums.WorkflowState.BoardAdministratorReview,       TimelineDays = 7,  IsOptional = false },
        new() { StepDescription = "Completed",                 WorkflowState = Enums.WorkflowState.Completed,                     TimelineDays = 0,  IsOptional = false },
    ];
}
