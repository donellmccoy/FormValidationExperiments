using FormValidationExperiments.Web.Enums;

namespace FormValidationExperiments.Web.Models;

/// <summary>
/// Class representing an appeal in the LOD process.
/// </summary>
public class LineOfDutyAppeal
{
    public DateTime AppealDate { get; set; }
    public string Appellant { get; set; } // Member or Next of Kin
    public List<string> NewEvidence { get; set; } = new List<string>();
    public LineOfDutyFinding OriginalFinding { get; set; }
    public LineOfDutyFinding AppealOutcome { get; set; }
    public LineOfDutyAuthority AppellateAuthority { get; set; }
    public DateTime? ResolutionDate { get; set; }
}
