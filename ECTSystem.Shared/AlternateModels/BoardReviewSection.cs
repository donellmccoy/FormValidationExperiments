using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part VI — Line of Duty Board Review (Items 28–33).
/// </summary>
public class BoardReviewSection
{
    // Item 28: Board Convened Date
    public DateTime? BoardConvenedDate { get; set; }

    // Item 29: Board Findings Narrative
    public string BoardFindings { get; set; }

    // Item 30: Board LOD Finding
    public FindingType? BoardLODFinding { get; set; }

    // Item 30a: Board Rationale
    public string BoardFindingRationale { get; set; }

    // Item 31: Board concurs with commander
    public bool? BoardConcursWithCommander { get; set; }

    public string BoardNonConcurrenceReason { get; set; }

    // Item 32: Board Member Names and Ranks
    public string BoardPresidentNameRank { get; set; }
    public string BoardMember1NameRank { get; set; }
    public string BoardMember2NameRank { get; set; }

    // Item 33: Board Signatures
    public SignatureBlockSection BoardPresidentSignature { get; set; } = new();
    public SignatureBlockSection BoardMember1Signature { get; set; } = new();
    public SignatureBlockSection BoardMember2Signature { get; set; } = new();
}
