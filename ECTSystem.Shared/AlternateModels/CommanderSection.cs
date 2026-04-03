using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part III — Unit Commander's Endorsement (Items 16–23).
/// </summary>
public class CommanderSection
{
    // Item 16: Duty Status at Time of Incident
    public DutyStatus? DutyStatus { get; set; }

    // Item 17: Proximate Cause of Incident
    public string ProximateCause { get; set; }

    // Item 18: Witnesses
    public List<string> WitnessNameAddresses { get; set; } = [];

    // Item 19a: Date Incident Occurred (DD Mmm YYYY)
    public DateTime? IncidentDate { get; set; }

    // Item 19b: Time
    public string IncidentTime { get; set; }

    // Item 19c: Place (Base/city, state/country)
    public string IncidentLocation { get; set; }

    // Item 19d: ARC Travel Status
    public bool? WasTravelingToFromDuty { get; set; }

    // Item 19e: ARC Travel Delay
    public bool? TravelDelayOccurred { get; set; }

    public string TravelDelayExplanation { get; set; }

    // Item 20a-e: Commander's Investigation
    public bool? MemberWearsProtectiveDevices { get; set; }

    public bool? ViolatedOrdersOrRegulations { get; set; }

    public string ViolatedOrdersExplanation { get; set; }

    public bool? IsApplicableMisconduct { get; set; }

    public string MisconductExplanation { get; set; }

    public bool? SafetyViolation { get; set; }

    public string SafetyViolationExplanation { get; set; }

    public string CommanderAdditionalInfo { get; set; }

    // Item 21: Commander's Recommendation
    public CommanderRecommendation? CommanderRecommendation { get; set; }

    // Item 22: Commander's Signature
    public SignatureBlockSection CommanderSignature { get; set; } = new();

    // Item 23: Commander's Comments
    public string CommanderComments { get; set; }
}
