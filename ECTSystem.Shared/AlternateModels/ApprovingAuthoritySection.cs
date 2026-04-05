using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part VII — Approving Authority (ARC only, Items 34–35).
/// </summary>
public class ApprovingAuthoritySection
{
    // Item 34: Approving Authority Finding
    public FindingType? ApprovingAuthorityFinding { get; set; }

    // Item 34a: Concurs with Wing Commander / Board
    public bool? ConcursWithLowerLevel { get; set; }

    public string NonConcurrenceReason { get; set; }

    // Item 34b: Rationale
    public string ApprovingAuthorityRationale { get; set; }

    // Item 35: Approving Authority Signature
    public SignatureBlockSection ApprovingAuthoritySignature { get; set; } = new();
}
