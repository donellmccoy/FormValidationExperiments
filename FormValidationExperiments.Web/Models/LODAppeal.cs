namespace AirForceLODSystem;

/// <summary>
/// Class representing an appeal in the LOD process.
/// </summary>
public class LODAppeal
{
    public DateTime AppealDate { get; set; }
    public string Appellant { get; set; } // Member or Next of Kin
    public List<string> NewEvidence { get; set; } = new List<string>();
    public LODFinding OriginalFinding { get; set; }
    public LODFinding AppealOutcome { get; set; }
    public LODAuthority AppellateAuthority { get; set; }
    public DateTime? ResolutionDate { get; set; }
}
