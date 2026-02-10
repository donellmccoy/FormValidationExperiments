using FormValidationExperiments.Shared.Enums;

namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Form model for the Commander Review step (AF Form 348, Items 16–23).
/// Captures the commander's endorsement, sources of information reviewed,
/// misconduct assessment, proximate cause analysis, and recommendation.
/// </summary>
public class CommanderReviewFormModel
{
    // ── Item 16: Sources of Information ──

    /// <summary>
    /// Gets or sets whether the member's written statement was reviewed. (Item 16)
    /// </summary>
    public bool MemberStatementReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether medical records and provider assessments were reviewed. (Item 16)
    /// </summary>
    public bool MedicalRecordsReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether witness statements were reviewed. (Item 16)
    /// </summary>
    public bool WitnessStatementsReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether police or Security Forces reports were reviewed. (Item 16)
    /// </summary>
    public bool PoliceReportsReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether the commander's report of investigation was reviewed. (Item 16)
    /// </summary>
    public bool CommanderReportReviewed { get; set; }

    /// <summary>
    /// Gets or sets whether other sources of information were reviewed. (Item 16)
    /// </summary>
    public bool OtherSourcesReviewed { get; set; }

    /// <summary>
    /// Gets or sets the description of other sources reviewed.
    /// Visible only when <see cref="OtherSourcesReviewed"/> is <see langword="true"/>. (Item 16)
    /// </summary>
    public string OtherSourcesDescription { get; set; } = string.Empty;

    // ── Item 17: Duty Status at Time of Incident ──

    /// <summary>
    /// Gets or sets the member's duty status at the time of the injury, disease, or death. (Item 17)
    /// </summary>
    public DutyStatus? DutyStatusAtTime { get; set; }

    // ── Item 18: Narrative of Circumstances ──

    /// <summary>
    /// Gets or sets the commander's brief narrative describing the circumstances
    /// of the injury, illness, or death. (Item 18)
    /// </summary>
    public string NarrativeOfCircumstances { get; set; } = string.Empty;

    // ── Item 19: Result of Misconduct ──

    /// <summary>
    /// Gets or sets whether the injury, illness, disease, or death resulted
    /// from the member's own misconduct. (Item 19)
    /// </summary>
    public bool? ResultOfMisconduct { get; set; }

    /// <summary>
    /// Gets or sets the explanation of misconduct.
    /// Visible only when <see cref="ResultOfMisconduct"/> is <see langword="true"/>. (Item 19)
    /// </summary>
    public string MisconductExplanation { get; set; } = string.Empty;

    // ── Item 20: Proximate Cause ──

    /// <summary>
    /// Gets or sets the proximate cause of the injury, disease, or death. (Item 20)
    /// </summary>
    public string ProximateCause { get; set; } = string.Empty;

    // ── Item 21: Commander's Recommendation ──

    /// <summary>
    /// Gets or sets the commander's LOD recommendation (e.g., ILOD, NILOD, Refer to Formal Investigation). (Item 21)
    /// </summary>
    public CommanderRecommendation? Recommendation { get; set; }

    /// <summary>
    /// Gets or sets any remarks supporting the commander's recommendation. (Item 21)
    /// </summary>
    public string RecommendationRemarks { get; set; } = string.Empty;

    // ── Items 22–23: Commander Signature Block ──

    /// <summary>
    /// Gets or sets the commander's full name for the signature block. (Item 22)
    /// </summary>
    public string CommanderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the commander's rank or grade. (Item 22)
    /// </summary>
    public MilitaryRank? CommanderRank { get; set; }

    /// <summary>
    /// Gets or sets the commander's organization or unit. (Item 22)
    /// </summary>
    public string CommanderOrganization { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date the commander signed the recommendation. (Item 23)
    /// </summary>
    public DateTime? CommanderSignatureDate { get; set; }
}
