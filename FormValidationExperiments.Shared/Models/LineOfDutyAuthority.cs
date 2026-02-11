namespace FormValidationExperiments.Shared.Models;

/// <summary>
/// Class representing an authority involved in the LOD process.
/// </summary>
public class LineOfDutyAuthority
{
    public int Id { get; set; }
    public int? LineOfDutyCaseId { get; set; }
    public string Role { get; set; } = string.Empty; // e.g., Immediate Commander, Appointing Authority, etc.
    public string Name { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty; // e.g., "Col", "Lt Col", "Maj"
    public string Title { get; set; } = string.Empty; // e.g., Wing CC, HQ AFRC/A1
    public DateTime? ActionDate { get; set; }
    public string Recommendation { get; set; } = string.Empty;
    public List<string> Comments { get; set; } = new List<string>();
}
