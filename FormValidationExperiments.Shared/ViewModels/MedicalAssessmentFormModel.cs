using FormValidationExperiments.Shared.Enums;

namespace FormValidationExperiments.Shared.ViewModels;

/// <summary>
/// Form model for the Medical Assessment step (AF Form 348, Items 9–15).
/// Captures clinical data, substance and toxicology information, psychiatric evaluation,
/// EPTS/NSA determination, ARC-specific fields, and the medical provider's signature.
/// </summary>
public class MedicalAssessmentFormModel
{
    // ── Item 9: Type of Investigation ──

    /// <summary>
    /// Gets or sets the type of investigation (e.g., Injury, Illness, Disease, Death). (Item 9)
    /// </summary>
    public IncidentType? InvestigationType { get; set; }

    // ── Item 10: Treatment Facility ──

    /// <summary>
    /// Gets or sets whether treatment was provided at a military medical facility. (Item 10)
    /// </summary>
    public bool? IsMilitaryFacility { get; set; }

    /// <summary>
    /// Gets or sets the name of the treatment facility.
    /// Visible only when <see cref="IsMilitaryFacility"/> is <see langword="true"/>. (Item 10)
    /// </summary>
    public string TreatmentFacilityName { get; set; } = string.Empty;

    // ── Item 11: Date/Time First Treated ──

    /// <summary>
    /// Gets or sets the date and time the member was first treated. (Item 11)
    /// </summary>
    public DateTime? TreatmentDateTime { get; set; }

    // ── Item 12: Clinical Diagnosis ──

    /// <summary>
    /// Gets or sets the clinical diagnosis. (Item 12)
    /// </summary>
    public string ClinicalDiagnosis { get; set; } = string.Empty;

    // ── Item 13: Under Influence of Drugs/Alcohol ──

    /// <summary>
    /// Gets or sets whether the member was under the influence of drugs or alcohol
    /// at the time of the incident. (Item 13)
    /// </summary>
    public bool? WasUnderInfluence { get; set; }

    /// <summary>
    /// Gets or sets the type of substance involved (Alcohol, Drugs, or Both).
    /// Visible only when <see cref="WasUnderInfluence"/> is <see langword="true"/>. (Item 13)
    /// </summary>
    public SubstanceType? SubstanceType { get; set; }

    // ── Item 13a: Toxicology Testing ──

    /// <summary>
    /// Gets or sets whether toxicology testing was performed. (Item 13a)
    /// </summary>
    public bool? ToxicologyTestDone { get; set; }

    /// <summary>
    /// Gets or sets the toxicology test results.
    /// Visible only when <see cref="ToxicologyTestDone"/> is <see langword="true"/>. (Item 13a)
    /// </summary>
    public string ToxicologyTestResults { get; set; } = string.Empty;

    // ── Item 14: Mental Responsibility ──

    /// <summary>
    /// Gets or sets whether the member was mentally responsible at the time of the incident. (Item 14)
    /// </summary>
    public bool? WasMentallyResponsible { get; set; }

    // ── Item 14a: Psychiatric Evaluation ──

    /// <summary>
    /// Gets or sets whether a psychiatric evaluation was completed. (Item 14a)
    /// </summary>
    public bool? PsychiatricEvalCompleted { get; set; }

    /// <summary>
    /// Gets or sets the date the psychiatric evaluation was completed.
    /// Visible only when <see cref="PsychiatricEvalCompleted"/> is <see langword="true"/>. (Item 14a)
    /// </summary>
    public DateTime? PsychiatricEvalDate { get; set; }

    /// <summary>
    /// Gets or sets the psychiatric evaluation results.
    /// Visible only when <see cref="PsychiatricEvalCompleted"/> is <see langword="true"/>. (Item 14a)
    /// </summary>
    public string PsychiatricEvalResults { get; set; } = string.Empty;

    // ── Item 14b: Other Relevant Conditions ──

    /// <summary>
    /// Gets or sets any other relevant medical conditions. (Item 14b)
    /// </summary>
    public string OtherRelevantConditions { get; set; } = string.Empty;

    // ── Item 14c: Other Tests ──

    /// <summary>
    /// Gets or sets whether other diagnostic tests were performed. (Item 14c)
    /// </summary>
    public bool? OtherTestsDone { get; set; }

    /// <summary>
    /// Gets or sets the date the other tests were performed.
    /// Visible only when <see cref="OtherTestsDone"/> is <see langword="true"/>. (Item 14c)
    /// </summary>
    public DateTime? OtherTestDate { get; set; }

    /// <summary>
    /// Gets or sets the results of the other tests.
    /// Visible only when <see cref="OtherTestsDone"/> is <see langword="true"/>. (Item 14c)
    /// </summary>
    public string OtherTestResults { get; set; } = string.Empty;

    // ── Item 15: EPTS/NSA ──

    /// <summary>
    /// Gets or sets whether this is an Existed Prior to Service – Not Service Aggravated
    /// (EPTS-NSA) condition. (Item 15)
    /// </summary>
    public bool? IsEptsNsa { get; set; }

    /// <summary>
    /// Gets or sets whether the pre-existing condition was aggravated by military service.
    /// Visible only when <see cref="IsEptsNsa"/> is <see langword="true"/>. (Item 15)
    /// </summary>
    public bool? IsServiceAggravated { get; set; }

    // ── Item 15a: Potentially Unfitting ──

    /// <summary>
    /// Gets or sets whether the condition is potentially unfitting for continued
    /// military service. (Item 15a)
    /// </summary>
    public bool? IsPotentiallyUnfitting { get; set; }

    // ── ARC-specific Fields ──

    /// <summary>
    /// Gets or sets whether the member was at a deployed location at the time of the incident.
    /// Applicable to Air Force Reserve (AFR) and Air National Guard (ANG) members.
    /// </summary>
    public bool? IsAtDeployedLocation { get; set; }

    /// <summary>
    /// Gets or sets whether this case requires an Air Reserve Component (ARC) board review.
    /// Visible only when <see cref="IsAtDeployedLocation"/> is <see langword="false"/>.
    /// </summary>
    public bool? RequiresArcBoard { get; set; }

    // ── Medical Findings / Recommendation ──

    /// <summary>
    /// Gets or sets the medical provider's clinical findings narrative.
    /// </summary>
    public string MedicalFindings { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the medical provider's recommendation.
    /// </summary>
    public string MedicalRecommendation { get; set; } = string.Empty;

    // ── Provider Signature Block ──

    /// <summary>
    /// Gets or sets the medical provider's full name for the signature block.
    /// </summary>
    public string ProviderName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the medical provider's rank or grade.
    /// </summary>
    public MilitaryRank? ProviderRank { get; set; }

    /// <summary>
    /// Gets or sets the date the medical provider signed the assessment.
    /// </summary>
    public DateTime? ProviderSignatureDate { get; set; }

    /// <summary>
    /// Gets or sets the medical provider's organization or unit.
    /// </summary>
    public string ProviderOrganization { get; set; } = string.Empty;
}
