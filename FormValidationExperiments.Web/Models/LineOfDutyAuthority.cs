namespace FormValidationExperiments.Web.Models;

/// <summary>
/// Class representing an authority involved in the LOD process.
/// </summary>
public class LineOfDutyAuthority
{
    public int Id { get; set; }
    public int? LineOfDutyCaseId { get; set; }
    public string Role { get; set; } // e.g., Immediate Commander, Appointing Authority, etc.
    public string Name { get; set; }
    public string Title { get; set; } // e.g., Wing CC, HQ AFRC/A1
    public DateTime? ActionDate { get; set; }
    public string Recommendation { get; set; }
    public List<string> Comments { get; set; } = new List<string>();
}
