using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part V — Wing Commander / Appointing Authority (Items 26–27).
/// </summary>
public class AppointingAuthoritySection
{
    // Item 26: Wing Commander's LOD Finding
    public FindingType? LODFinding { get; set; }

    // Item 26a: Rationale for Finding
    public string FindingRationale { get; set; }

    // Item 26b: Concurrency with lower-level recommendations
    public bool? ConcursWithCommander { get; set; }

    public string NonConcurrenceRationale { get; set; }

    // Item 26c: Formal investigation required
    public bool? FormalInvestigationRequired { get; set; }

    // Item 27: Appointing Authority Signature
    public SignatureBlockSection AppointingAuthoritySignature { get; set; } = new();
}
