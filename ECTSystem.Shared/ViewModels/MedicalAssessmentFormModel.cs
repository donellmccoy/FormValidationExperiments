using System.ComponentModel.DataAnnotations;

using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

/// <summary>
/// Form model for the Medical Assessment step (AF Form 348, Items 9–15).
/// Captures clinical data, substance and toxicology information, psychiatric evaluation,
/// EPTS/NSA determination, ARC-specific fields, and the medical provider's signature.
/// </summary>
public class MedicalAssessmentFormModel : TrackableModel, IValidatableObject
{
    // ── Item 9: Type of Investigation ──

    /// <summary>
    /// Gets or sets the type of investigation (e.g., Injury, Illness, Disease, Death). (Item 9)
    /// </summary>
    [Required(ErrorMessage = "Type of investigation is required.")]
    public IncidentType? InvestigationType { get; set; }

    // ── Item 10: Treatment Facility ──

    /// <summary>
    /// Gets or sets whether treatment was provided at a military medical facility. (Item 10)
    /// </summary>
    [Required(ErrorMessage = "Military facility selection is required.")]
    public bool? IsMilitaryFacility { get; set; }

    /// <summary>
    /// Gets or sets the name of the treatment facility.
    /// Conditionally required when <see cref="IsMilitaryFacility"/> is <see langword="true"/>. (Item 10)
    /// </summary>
    [StringLength(200)]
    public string TreatmentFacilityName { get; set; } = string.Empty;

    // ── Item 11: Date/Time First Treated ──

    /// <summary>
    /// Gets or sets the date and time the member was first treated. (Item 11)
    /// </summary>
    [Required(ErrorMessage = "Treatment date/time is required.")]
    public DateTime? TreatmentDateTime { get; set; }

    // ── Item 12: Clinical Diagnosis ──

    /// <summary>
    /// Gets or sets the clinical diagnosis. (Item 12)
    /// </summary>
    [Required(ErrorMessage = "Clinical diagnosis is required.")]
    [StringLength(2000)]
    public string ClinicalDiagnosis { get; set; } = string.Empty;

    // ── Item 13: Under Influence of Drugs/Alcohol ──

    /// <summary>
    /// Gets or sets whether the member was under the influence of drugs or alcohol
    /// at the time of the incident. (Item 13)
    /// </summary>
    [Required(ErrorMessage = "Under influence selection is required.")]
    public bool? WasUnderInfluence { get; set; }

    /// <summary>
    /// Gets or sets the type of substance involved (Alcohol, Drugs, or Both).
    /// Conditionally required when <see cref="WasUnderInfluence"/> is <see langword="true"/>. (Item 13)
    /// </summary>
    public SubstanceType? SubstanceType { get; set; }

    // ── Item 13a: Toxicology Testing ──

    /// <summary>
    /// Gets or sets whether toxicology testing was performed. (Item 13a)
    /// </summary>
    [Required(ErrorMessage = "Toxicology test selection is required.")]
    public bool? ToxicologyTestDone { get; set; }

    /// <summary>
    /// Gets or sets the toxicology test results.
    /// Conditionally required when <see cref="ToxicologyTestDone"/> is <see langword="true"/>. (Item 13a)
    /// </summary>
    public string ToxicologyTestResults { get; set; } = string.Empty;

    // ── Item 14: Mental Responsibility ──

    /// <summary>
    /// Gets or sets whether the member was mentally responsible at the time of the incident. (Item 14)
    /// </summary>
    [Required(ErrorMessage = "Mental responsibility selection is required.")]
    public bool? WasMentallyResponsible { get; set; }

    // ── Item 14a: Psychiatric Evaluation ──

    /// <summary>
    /// Gets or sets whether a psychiatric evaluation was completed. (Item 14a)
    /// </summary>
    [Required(ErrorMessage = "Psychiatric evaluation selection is required.")]
    public bool? PsychiatricEvalCompleted { get; set; }

    /// <summary>
    /// Gets or sets the date the psychiatric evaluation was completed.
    /// Conditionally required when <see cref="PsychiatricEvalCompleted"/> is <see langword="true"/>. (Item 14a)
    /// </summary>
    public DateTime? PsychiatricEvalDate { get; set; }

    /// <summary>
    /// Gets or sets the psychiatric evaluation results.
    /// Conditionally required when <see cref="PsychiatricEvalCompleted"/> is <see langword="true"/>. (Item 14a)
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
    [Required(ErrorMessage = "Other tests selection is required.")]
    public bool? OtherTestsDone { get; set; }

    /// <summary>
    /// Gets or sets the date the other tests were performed.
    /// Conditionally required when <see cref="OtherTestsDone"/> is <see langword="true"/>. (Item 14c)
    /// </summary>
    public DateTime? OtherTestDate { get; set; }

    /// <summary>
    /// Gets or sets the results of the other tests.
    /// Conditionally required when <see cref="OtherTestsDone"/> is <see langword="true"/>. (Item 14c)
    /// </summary>
    public string OtherTestResults { get; set; } = string.Empty;

    // ── Item 15: EPTS/NSA ──

    /// <summary>
    /// Gets or sets whether this is an Existed Prior to Service – Not Service Aggravated
    /// (EPTS-NSA) condition. (Item 15)
    /// </summary>
    [Required(ErrorMessage = "EPTS/NSA selection is required.")]
    public bool? IsEptsNsa { get; set; }

    /// <summary>
    /// Gets or sets whether the pre-existing condition was aggravated by military service.
    /// Conditionally required when <see cref="IsEptsNsa"/> is <see langword="true"/>. (Item 15)
    /// </summary>
    public bool? IsServiceAggravated { get; set; }

    // ── Item 15a: Potentially Unfitting ──

    /// <summary>
    /// Gets or sets whether the condition is potentially unfitting for continued
    /// military service. (Item 15a)
    /// </summary>
    [Required(ErrorMessage = "Potentially unfitting selection is required.")]
    public bool? IsPotentiallyUnfitting { get; set; }

    // ── ARC-specific Fields ──

    /// <summary>
    /// Gets or sets whether the member was at a deployed location at the time of the incident.
    /// Applicable to Air Force Reserve (AFR) and Air National Guard (ANG) members.
    /// </summary>
    public bool? IsAtDeployedLocation { get; set; }

    /// <summary>
    /// Gets or sets whether this case requires an Air Reserve Component (ARC) board review.
    /// Conditionally required when <see cref="IsAtDeployedLocation"/> is <see langword="false"/>.
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
    [Required(ErrorMessage = "Medical recommendation is required.")]
    [StringLength(4000)]
    public string MedicalRecommendation { get; set; } = string.Empty;

    // ── Conditional & Date Validation ──

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsMilitaryFacility == true && string.IsNullOrWhiteSpace(TreatmentFacilityName))
            yield return new ValidationResult("Facility name is required when treatment was at a military facility.", [nameof(TreatmentFacilityName)]);

        if (WasUnderInfluence == true && SubstanceType == null)
            yield return new ValidationResult("Substance type is required when member was under the influence.", [nameof(SubstanceType)]);

        if (ToxicologyTestDone == true && string.IsNullOrWhiteSpace(ToxicologyTestResults))
            yield return new ValidationResult("Toxicology test results are required when testing was performed.", [nameof(ToxicologyTestResults)]);

        if (PsychiatricEvalCompleted == true)
        {
            if (PsychiatricEvalDate == null)
                yield return new ValidationResult("Psychiatric evaluation date is required.", [nameof(PsychiatricEvalDate)]);

            if (string.IsNullOrWhiteSpace(PsychiatricEvalResults))
                yield return new ValidationResult("Psychiatric evaluation results are required.", [nameof(PsychiatricEvalResults)]);
        }

        if (OtherTestsDone == true)
        {
            if (OtherTestDate == null)
                yield return new ValidationResult("Other test date is required.", [nameof(OtherTestDate)]);

            if (string.IsNullOrWhiteSpace(OtherTestResults))
                yield return new ValidationResult("Other test results are required.", [nameof(OtherTestResults)]);
        }

        if (IsEptsNsa == true && IsServiceAggravated == null)
            yield return new ValidationResult("Service aggravation determination is required for EPTS conditions.", [nameof(IsServiceAggravated)]);

        if (IsAtDeployedLocation == false && RequiresArcBoard == null)
            yield return new ValidationResult("ARC board review determination is required.", [nameof(RequiresArcBoard)]);

        if (TreatmentDateTime > DateTime.Now)
            yield return new ValidationResult("Treatment date cannot be in the future.", [nameof(TreatmentDateTime)]);

        if (PsychiatricEvalDate > DateTime.Now)
            yield return new ValidationResult("Psychiatric evaluation date cannot be in the future.", [nameof(PsychiatricEvalDate)]);

        if (OtherTestDate > DateTime.Now)
            yield return new ValidationResult("Other test date cannot be in the future.", [nameof(OtherTestDate)]);
    }
}
