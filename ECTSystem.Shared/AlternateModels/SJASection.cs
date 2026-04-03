namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part IV — Staff Judge Advocate Review (Items 24–25).
/// </summary>
public class SJASection
{
    // Item 24: Legal Sufficiency Review
    public bool? IsLegallySufficient { get; set; }

    // Item 24 supporting: SJA comments/recommendations
    public string SJAComments { get; set; }

    public string SJARecommendation { get; set; }

    // Item 24: Concurs with Commander
    public bool? SJAConcurs { get; set; }

    public string NonConcurrenceReason { get; set; }

    // Item 25: SJA Signature
    public SignatureBlockSection SJASignature { get; set; } = new();

    // Conditional visibility
    public bool ShowNonConcurrenceReason => SJAConcurs == false;
}
