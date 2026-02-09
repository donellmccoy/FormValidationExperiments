namespace AirForceLODSystem;

/// <summary>
/// Class representing a timeline step in the LOD process.
/// </summary>
public class TimelineStep
{
    public string StepDescription { get; set; } // e.g., "Member Reports", "Medical Provider Review"
    public int TimelineDays { get; set; } // e.g., 5 calendar days
    public DateTime? StartDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public bool IsOptional { get; set; }
    public LODAuthority ResponsibleAuthority { get; set; }
}
