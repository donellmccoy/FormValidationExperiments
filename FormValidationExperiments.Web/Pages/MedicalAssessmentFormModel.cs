using AirForceLODSystem;

namespace FormValidationExperiments.Web.Pages;

public partial class Home
{
    public class MedicalAssessmentFormModel
    {
        // ── Item 9: Type of Investigation ──
        public IncidentType? InvestigationType { get; set; }

        // ── Item 10: Treatment Facility ──
        public bool? IsMilitaryFacility { get; set; }
        public string TreatmentFacilityName { get; set; } = string.Empty;

        // ── Item 11: Date/Time First Treated ──
        public DateTime? TreatmentDateTime { get; set; }

        // ── Item 12: Clinical Diagnosis ──
        public string ClinicalDiagnosis { get; set; } = string.Empty;

        // ── Item 13: Under Influence of Drugs/Alcohol ──
        public bool? WasUnderInfluence { get; set; }
        public SubstanceType? SubstanceType { get; set; }

        // ── Item 13a: Toxicology Testing ──
        public bool? ToxicologyTestDone { get; set; }
        public string ToxicologyTestResults { get; set; } = string.Empty;

        // ── Item 14: Mental Responsibility ──
        public bool? WasMentallyResponsible { get; set; }

        // ── Item 14a: Psychiatric Evaluation ──
        public bool? PsychiatricEvalCompleted { get; set; }
        public DateTime? PsychiatricEvalDate { get; set; }
        public string PsychiatricEvalResults { get; set; } = string.Empty;

        // ── Item 14b: Other Relevant Conditions ──
        public string OtherRelevantConditions { get; set; } = string.Empty;

        // ── Item 14c: Other Tests ──
        public bool? OtherTestsDone { get; set; }
        public DateTime? OtherTestDate { get; set; }
        public string OtherTestResults { get; set; } = string.Empty;

        // ── Item 15: EPTS/NSA ──
        public bool? IsEptsNsa { get; set; }
        public bool? IsServiceAggravated { get; set; }

        // ── Item 15a: Potentially Unfitting ──
        public bool? IsPotentiallyUnfitting { get; set; }

        // ── ARC-specific Fields ──
        public bool? IsAtDeployedLocation { get; set; }
        public bool? RequiresArcBoard { get; set; }

        // ── Medical Findings / Recommendation ──
        public string MedicalFindings { get; set; } = string.Empty;
        public string MedicalRecommendation { get; set; } = string.Empty;

        // ── Provider Signature Block ──
        public string ProviderNameAndRank { get; set; } = string.Empty;
        public DateTime? ProviderSignatureDate { get; set; }
    }
}
