using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Class representing a timeline step in the LOD process.
/// </summary>
public class TimelineStep : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string StepDescription { get; set; } = string.Empty; // e.g., "Member Reports", "Medical Provider Review"
    public int TimelineDays { get; set; } // e.g., 5 calendar days
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public DateTime? SignedDate { get; set; }
    public string SignedBy { get; set; } = string.Empty;
    public bool IsOptional { get; set; }
    public LineOfDutyWorkflowState? WorkflowState { get; set; }
    public int? ResponsibleAuthorityId { get; set; }
    public LineOfDutyAuthority ResponsibleAuthority { get; set; }
}
