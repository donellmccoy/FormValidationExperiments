namespace FormValidationExperiments.Shared.Models;

/// <summary>
/// Class representing INCAP (Incapacitation) Pay details.
/// </summary>
public class INCAPDetails
{
    public int Id { get; set; }
    public bool IsEligible { get; set; }
    public decimal CivilianIncomeLoss { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Documentation { get; set; } = string.Empty; // Proof of income loss
}
