namespace AirForceLODSystem;

/// <summary>
/// Class representing INCAP (Incapacitation) Pay details.
/// </summary>
public class INCAPDetails
{
    public bool IsEligible { get; set; }
    public decimal CivilianIncomeLoss { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Documentation { get; set; } // Proof of income loss
}
