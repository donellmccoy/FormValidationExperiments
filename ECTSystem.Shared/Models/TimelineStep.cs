using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Describes a single step in the LOD workflow timeline, including its
/// expected duration and whether it is optional.
/// </summary>
public class TimelineStep
{
    public string StepDescription { get; set; } = string.Empty;
    public int TimelineDays { get; set; }
    public bool IsOptional { get; set; }
    public WorkflowState? WorkflowState { get; set; }
}
