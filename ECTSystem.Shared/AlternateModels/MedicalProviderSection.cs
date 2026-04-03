using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.AlternateModels;

/// <summary>
/// AF Form 348 Part II — Military Medical Provider (Items 9–15).
/// </summary>
public class MedicalProviderSection
{
    // Item 9: Investigation Of
    public IncidentType? InvestigationType { get; set; }

    // Item 10a: Military or Civilian facility
    public bool? IsMilitaryFacility { get; set; }

    public string TreatmentFacilityName { get; set; }

    // Item 10c: Treatment Provided On
    public DateTime? TreatmentDateTime { get; set; }

    // Item 11: Description of Symptoms and Diagnosis
    public string SymptomsAndDiagnosis { get; set; }

    // Item 12: Details of Death, Injury, Illness or History of Disease
    public string DetailsOfIncident { get; set; }

    // Item 13a: Under the influence
    public bool? WasUnderInfluence { get; set; }

    public SubstanceType? SubstanceType { get; set; }

    // Item 13b: Alcohol/Drug Test
    public bool? ToxicologyTestDone { get; set; }

    public string ToxicologyTestResults { get; set; }

    // Item 13c: Mentally Responsible
    public bool? WasMentallyResponsible { get; set; }

    // Item 13d: Psychiatric Evaluation
    public bool? PsychiatricEvalCompleted { get; set; }

    public DateTime? PsychiatricEvalDate { get; set; }
    public string PsychiatricEvalResults { get; set; }

    // Item 13e: Other Relevant Conditions
    public string OtherRelevantConditions { get; set; }

    // Item 13f: Other Tests
    public bool? OtherTestsDone { get; set; }

    public DateTime? OtherTestDate { get; set; }
    public string OtherTestResults { get; set; }

    // Item 14a-e: ARC-specific fields
    public bool? IsAtDeployedLocation { get; set; }
    public bool? IsEptsNsa { get; set; }
    public bool? IsServiceAggravated { get; set; }
    public bool? IsPotentiallyUnfitting { get; set; }
    public bool? RequiresArcBoard { get; set; }

    // Item 14 supporting
    public string MedicalFindings { get; set; }

    public string MedicalRecommendation { get; set; }

    public string SARCCoordination { get; set; }

    // Item 15: Provider Signature
    public SignatureBlockSection ProviderSignature { get; set; } = new();
}