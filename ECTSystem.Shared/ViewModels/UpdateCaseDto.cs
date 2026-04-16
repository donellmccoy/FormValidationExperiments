using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class UpdateCaseDto
{
    // Basic Case Information
    [Required]
    public ProcessType ProcessType { get; set; }

    [Required]
    public ServiceComponent Component { get; set; }

    [Required]
    [StringLength(200)]
    public string MemberName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string MemberRank { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    public string ServiceNumber { get; set; } = string.Empty;

    public DateTime? MemberDateOfBirth { get; set; }

    [StringLength(100)]
    public string Unit { get; set; } = string.Empty;

    [StringLength(200)]
    public string FromLine { get; set; } = string.Empty;

    [Required]
    public IncidentType IncidentType { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    [Required]
    [StringLength(4000)]
    public string IncidentDescription { get; set; } = string.Empty;

    [Required]
    public DutyStatus IncidentDutyStatus { get; set; }

    // Part I: Orders / Duty Period
    [StringLength(4)]
    public string MemberOrdersStartTime { get; set; } = string.Empty;

    public DateTime? MemberOrdersEndDate { get; set; }

    [StringLength(4)]
    public string MemberOrdersEndTime { get; set; } = string.Empty;

    // Part I Item 2: Type of Medical Unit Reporting
    public bool IsMTF { get; set; }
    public bool IsRMU { get; set; }
    public bool IsGMU { get; set; }
    public bool IsDeployedLocation { get; set; }

    // Part I Item 8: Additional component types
    public bool IsUSAFA { get; set; }
    public bool IsAFROTC { get; set; }

    // Medical Assessment Fields
    public bool? IsMilitaryFacility { get; set; }

    [StringLength(200)]
    public string TreatmentFacilityName { get; set; } = string.Empty;

    public DateTime? TreatmentDateTime { get; set; }

    [StringLength(4000)]
    public string ClinicalDiagnosis { get; set; } = string.Empty;

    [StringLength(4000)]
    public string MedicalFindings { get; set; } = string.Empty;

    public bool? WasUnderInfluence { get; set; }
    public SubstanceType? SubstanceType { get; set; }
    public bool? WasMentallyResponsible { get; set; }
    public bool? PsychiatricEvalCompleted { get; set; }
    public DateTime? PsychiatricEvalDate { get; set; }

    [StringLength(4000)]
    public string PsychiatricEvalResults { get; set; } = string.Empty;

    [StringLength(4000)]
    public string OtherRelevantConditions { get; set; } = string.Empty;

    public bool? OtherTestsDone { get; set; }
    public DateTime? OtherTestDate { get; set; }

    [StringLength(4000)]
    public string OtherTestResults { get; set; } = string.Empty;

    public bool? IsServiceAggravated { get; set; }
    public bool? IsPotentiallyUnfitting { get; set; }
    public bool? IsAtDeployedLocation { get; set; }
    public bool? RequiresArcBoard { get; set; }

    [StringLength(4000)]
    public string MedicalRecommendation { get; set; } = string.Empty;

    // Commander Review — Sources of Information
    public bool MemberStatementReviewed { get; set; }
    public bool MedicalRecordsReviewed { get; set; }
    public bool WitnessStatementsReviewed { get; set; }
    public bool PoliceReportsReviewed { get; set; }
    public bool CommanderReportReviewed { get; set; }
    public bool OsiReportsReviewed { get; set; }
    public bool MilitaryPoliceReportsReviewed { get; set; }
    public bool OtherSourcesReviewed { get; set; }

    [StringLength(2000)]
    public string OtherSourcesDescription { get; set; } = string.Empty;

    [StringLength(4000)]
    public string MisconductExplanation { get; set; } = string.Empty;

    [StringLength(200)]
    public string CommanderToLine { get; set; } = string.Empty;

    [StringLength(200)]
    public string CommanderFromLine { get; set; } = string.Empty;

    // Commander Review — Item 19: Duty Status at Time of Incident
    public bool WasPresentForDuty { get; set; }
    public bool WasOnDuty { get; set; }
    public bool WasOnIDT { get; set; }
    public bool WasAbsentWithLeave { get; set; }
    public bool WasAbsentWithoutLeave { get; set; }

    [StringLength(50)]
    public string AbsentWithoutLeaveDate1 { get; set; } = string.Empty;

    [StringLength(10)]
    public string AbsentWithoutLeaveTime1 { get; set; } = string.Empty;

    [StringLength(50)]
    public string AbsentWithoutLeaveDate2 { get; set; } = string.Empty;

    [StringLength(10)]
    public string AbsentWithoutLeaveTime2 { get; set; } = string.Empty;

    // Commander Review — Item 21: Witnesses
    [StringLength(500)]
    public string WitnessNameAddress1 { get; set; } = string.Empty;

    [StringLength(500)]
    public string WitnessNameAddress2 { get; set; } = string.Empty;

    [StringLength(500)]
    public string WitnessNameAddress3 { get; set; } = string.Empty;

    [StringLength(500)]
    public string WitnessNameAddress4 { get; set; } = string.Empty;

    [StringLength(500)]
    public string WitnessNameAddress5 { get; set; } = string.Empty;

    // Process Details
    public DateTime InitiationDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int TotalTimelineDays { get; set; }
    public bool IsInterimLOD { get; set; }
    public DateTime? InterimLODExpiration { get; set; }

    // Findings and Determinations
    public FindingType FinalFinding { get; set; }

    [StringLength(4000)]
    public string ProximateCause { get; set; } = string.Empty;

    public bool IsPriorServiceCondition { get; set; }

    [StringLength(4000)]
    public string PSCDocumentation { get; set; } = string.Empty;

    public bool EightYearRuleApplies { get; set; }
    public int YearsOfService { get; set; }

    // Signatures and Dates — Part II Provider (Item 15)
    [StringLength(200)]
    public string ProviderNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string ProviderDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string ProviderSignature { get; set; } = string.Empty;

    // Signatures and Dates — Part III Commander (Item 23)
    [StringLength(200)]
    public string CommanderNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string CommanderDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string CommanderSignature { get; set; } = string.Empty;

    // Part IV: SJA/Legal Review (Items 24–25)
    [StringLength(200)]
    public string SjaNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string SjaDate { get; set; } = string.Empty;

    public bool SjaConcurs { get; set; }

    // Signatures and Dates — Part V Wing CC / Appointing Authority (Items 25–27)
    [StringLength(500)]
    public string WingCcSignature { get; set; } = string.Empty;

    [StringLength(200)]
    public string AppointingAuthorityNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string AppointingAuthorityDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string AppointingAuthoritySignature { get; set; } = string.Empty;

    // Part VI: Formal Board Review (Items 28–33)
    [StringLength(4000)]
    public string MedicalReviewText { get; set; } = string.Empty;

    [StringLength(200)]
    public string MedicalReviewerNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string MedicalReviewDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string MedicalReviewerSignature { get; set; } = string.Empty;

    [StringLength(4000)]
    public string LegalReviewText { get; set; } = string.Empty;

    [StringLength(200)]
    public string LegalReviewerNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string LegalReviewDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string LegalReviewerSignature { get; set; } = string.Empty;

    [StringLength(200)]
    public string LodBoardChairNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string LodBoardChairDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string LodBoardChairSignature { get; set; } = string.Empty;

    public FindingType? BoardFinding { get; set; }
    public bool BoardReferForFormal { get; set; }

    // Part VII: Approving Authority (Items 34–35)
    [StringLength(200)]
    public string ApprovingAuthorityNameRank { get; set; } = string.Empty;

    [StringLength(50)]
    public string ApprovingAuthorityDate { get; set; } = string.Empty;

    [StringLength(500)]
    public string ApprovingAuthoritySignature { get; set; } = string.Empty;

    public FindingType? ApprovingFinding { get; set; }
    public bool ApprovingReferForFormal { get; set; }

    // Notification & Reporting
    public bool? NotifiedMedicalUnitTimely { get; set; }
    public bool? SubmittedMedicalDocumentsTimely { get; set; }

    // Special Handling
    public bool? IsSexualAssaultCase { get; set; }
    public bool? RestrictedReporting { get; set; }

    [StringLength(2000)]
    public string SARCCoordination { get; set; } = string.Empty;

    [StringLength(4000)]
    public string ToxicologyReport { get; set; } = string.Empty;

    // Related Benefits
    public int? MEDCONId { get; set; }
    public int? INCAPId { get; set; }
    public bool MemberChoseMEDCON { get; set; }

    // Member FK
    [Required]
    [Range(1, int.MaxValue)]
    public int MemberId { get; set; }

    // Audit and Notes
    public bool IsAudited { get; set; }

    [StringLength(200)]
    public string PointOfContact { get; set; } = string.Empty;
}
