namespace FormValidationExperiments.Shared.Models;

/// <summary>
/// Class representing a timeline step in the LOD process.
/// </summary>
public class TimelineStep
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public string StepDescription { get; set; } = string.Empty; // e.g., "Member Reports", "Medical Provider Review"
    public int TimelineDays { get; set; } // e.g., 5 calendar days
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool IsOptional { get; set; }
    public int? ResponsibleAuthorityId { get; set; }
    public LineOfDutyAuthority? ResponsibleAuthority { get; set; }
}
