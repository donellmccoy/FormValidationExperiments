using FormValidationExperiments.Shared.Enums;

namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Form model for the Legal / Staff Judge Advocate (SJA) Review step
/// (AF Form 348, Items 24–25). Captures legal sufficiency determination,
/// concurrence with the commander's recommendation, and SJA signature.
/// </summary>
public class LegalSJAReviewFormModel
{
    // ── Item 24: Legal Sufficiency Review ──

    /// <summary>
    /// Gets or sets whether the LOD determination package is legally sufficient. (Item 24)
    /// </summary>
    public bool? IsLegallySufficient { get; set; }

    /// <summary>
    /// Gets or sets whether the SJA concurs with the commander's recommendation. (Item 24)
    /// </summary>
    public bool? ConcurWithRecommendation { get; set; }

    /// <summary>
    /// Gets or sets the basis for non-concurrence.
    /// Visible only when <see cref="ConcurWithRecommendation"/> is <see langword="false"/>. (Item 24)
    /// </summary>
    public string NonConcurrenceReason { get; set; } = string.Empty;

    // ── Item 25: Legal Remarks ──

    /// <summary>
    /// Gets or sets the SJA's legal remarks or opinion. (Item 25)
    /// </summary>
    public string LegalRemarks { get; set; } = string.Empty;

    // ── SJA Signature Block ──

    /// <summary>
    /// Gets or sets the SJA's full name for the signature block.
    /// </summary>
    public string SJAName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SJA's rank or grade.
    /// </summary>
    public MilitaryRank? SJARank { get; set; }

    /// <summary>
    /// Gets or sets the SJA's organization or unit.
    /// </summary>
    public string SJAOrganization { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the SJA signed the review.
    /// </summary>
    public DateTime? SJASignatureDate { get; set; }
}
