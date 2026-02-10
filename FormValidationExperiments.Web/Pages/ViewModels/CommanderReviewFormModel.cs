using AirForceLODSystem;

namespace FormValidationExperiments.Web.Pages.ViewModels;

public class CommanderReviewFormModel
{
    // ── Item 16: Sources of Information ──
    public bool MemberStatementReviewed { get; set; }
    public bool MedicalRecordsReviewed { get; set; }
    public bool WitnessStatementsReviewed { get; set; }
    public bool PoliceReportsReviewed { get; set; }
    public bool CommanderReportReviewed { get; set; }
    public bool OtherSourcesReviewed { get; set; }
    public string OtherSourcesDescription { get; set; } = string.Empty;

    // ── Item 17: Duty Status at Time of Incident ──
    public DutyStatus? DutyStatusAtTime { get; set; }

    // ── Item 18: Narrative of Circumstances ──
    public string NarrativeOfCircumstances { get; set; } = string.Empty;

    // ── Item 19: Result of Misconduct ──
    public bool? ResultOfMisconduct { get; set; }
    public string MisconductExplanation { get; set; } = string.Empty;

    // ── Item 20: Proximate Cause ──
    public string ProximateCause { get; set; } = string.Empty;

    // ── Item 21: Commander's Recommendation ──
    public CommanderRecommendation? Recommendation { get; set; }
    public string RecommendationRemarks { get; set; } = string.Empty;

    // ── Items 22-23: Commander Signature Block ──
    public string CommanderName { get; set; } = string.Empty;
    public MilitaryRank? CommanderRank { get; set; }
    public string CommanderOrganization { get; set; } = string.Empty;
    public DateTime? CommanderSignatureDate { get; set; }
}
