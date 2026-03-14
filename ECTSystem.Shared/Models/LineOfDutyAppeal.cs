using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Class representing an appeal in the LOD process.
/// </summary>
public class LineOfDutyAppeal : AuditableEntity
{
    public int Id { get; set; }
    public int LineOfDutyCaseId { get; set; }
    public DateTime AppealDate { get; set; }
    public string Appellant { get; set; } = string.Empty; // Member or Next of Kin
    public List<string> NewEvidence { get; set; } = new List<string>();
    public LineOfDutyFinding OriginalFinding { get; set; }
    public LineOfDutyFinding AppealOutcome { get; set; }
    public int? AppellateAuthorityId { get; set; }
    public LineOfDutyAuthority AppellateAuthority { get; set; }
    public DateTime? ResolutionDate { get; set; }
}
