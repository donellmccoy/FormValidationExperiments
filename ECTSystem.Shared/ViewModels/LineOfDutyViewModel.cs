using System.ComponentModel.DataAnnotations;
using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.ViewModels;

public class LineOfDutyViewModel : TrackableModel, IValidatableObject
{
    [FormSection("CaseInfo")]
    public string CaseNumber { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string MemberName { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string Component { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string Rank { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string Grade { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string Unit { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public string DateOfInjury { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    [FormSection("MemberInfo")]
    public string SSN { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public string DutyStatus { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public string Status { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public WorkflowState WorkflowState { get; set; }

    [FormSection("CaseInfo")]
    public string IncidentCircumstances { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public string ReportedInjury { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public string RequestingCommander { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public string MedicalProvider { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public DateTime? ReportDate { get; set; }

    [FormSection("MemberInfo")]
    public string LastName { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public string FirstName { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public string MiddleInitial { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public DateTime? DateOfBirth { get; set; }

    [FormSection("MemberInfo")]
    public string OrganizationUnit { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public string MemberStatus { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public bool? NotifiedMedicalUnitTimely { get; set; }

    [FormSection("MemberInfo")]
    public bool? SubmittedMedicalDocumentsTimely { get; set; }

    [FormSection("MemberInfo")]
    public bool? InvolvesSexualAssault { get; set; }

    [FormSection("MemberInfo")]
    public bool? IsRestrictedReport { get; set; }

    [FormSection("MemberInfo")]
    public string MemberOrdersStartTime { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public DateTime? MemberOrdersEndDate { get; set; }

    [FormSection("MemberInfo")]
    public string MemberOrdersEndTime { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public bool IsMTF { get; set; }

    [FormSection("MemberInfo")]
    public bool IsRMU { get; set; }

    [FormSection("MemberInfo")]
    public bool IsGMU { get; set; }

    [FormSection("MemberInfo")]
    public bool IsDeployedLocation { get; set; }

    [FormSection("MemberInfo")]
    public bool IsUSAFA { get; set; }

    [FormSection("MemberInfo")]
    public bool IsAFROTC { get; set; }

    [FormSection("MemberInfo")]
    public string FromLine { get; set; } = string.Empty;

    [FormSection("MemberInfo")]
    public ProcessType? ProcessType { get; set; }

    [FormSection("MemberInfo")]
    public bool IsInterimLOD { get; set; }

    [FormSection("MemberInfo")]
    public DateTime? InterimLODExpiration { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Type of investigation is required.")]
    public IncidentType? InvestigationType { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Military facility selection is required.")]
    public bool? IsMilitaryFacility { get; set; }

    [FormSection("MedicalAssessment")]
    [StringLength(200)]
    public string TreatmentFacilityName { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Treatment date/time is required.")]
    public DateTime? TreatmentDateTime { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Clinical diagnosis is required.")]
    [StringLength(2000)]
    public string ClinicalDiagnosis { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Under influence selection is required.")]
    public bool? WasUnderInfluence { get; set; }

    [FormSection("MedicalAssessment")]
    public SubstanceType? SubstanceType { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Toxicology test selection is required.")]
    public bool? ToxicologyTestDone { get; set; }

    [FormSection("MedicalAssessment")]
    public string ToxicologyTestResults { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Mental responsibility selection is required.")]
    public bool? WasMentallyResponsible { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Psychiatric evaluation selection is required.")]
    public bool? PsychiatricEvalCompleted { get; set; }

    [FormSection("MedicalAssessment")]
    public DateTime? PsychiatricEvalDate { get; set; }

    [FormSection("MedicalAssessment")]
    public string PsychiatricEvalResults { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    public string OtherRelevantConditions { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Other tests selection is required.")]
    public bool? OtherTestsDone { get; set; }

    [FormSection("MedicalAssessment")]
    public DateTime? OtherTestDate { get; set; }

    [FormSection("MedicalAssessment")]
    public string OtherTestResults { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "EPTS/NSA selection is required.")]
    public bool? IsEptsNsa { get; set; }

    [FormSection("MedicalAssessment")]
    public bool? IsServiceAggravated { get; set; }

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Potentially unfitting selection is required.")]
    public bool? IsPotentiallyUnfitting { get; set; }

    [FormSection("MedicalAssessment")]
    public bool? IsAtDeployedLocation { get; set; }

    [FormSection("MedicalAssessment")]
    public bool? RequiresArcBoard { get; set; }

    [FormSection("MedicalAssessment")]
    public string MedicalFindings { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    [Required(ErrorMessage = "Medical recommendation is required.")]
    [StringLength(4000)]
    public string MedicalRecommendation { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    public string ProviderDate { get; set; } = string.Empty;

    [FormSection("MedicalAssessment")]
    public string SARCCoordination { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public bool MemberStatementReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool MedicalRecordsReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool WitnessStatementsReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool PoliceReportsReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool CommanderReportReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool OtherSourcesReviewed { get; set; }

    [FormSection("UnitCommander")]
    public string OtherSourcesDescription { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public DutyStatus? DutyStatusAtTime { get; set; }

    [FormSection("UnitCommander")]
    public string NarrativeOfCircumstances { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public bool? ResultOfMisconduct { get; set; }

    [FormSection("UnitCommander")]
    public string MisconductExplanation { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string ProximateCause { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public CommanderRecommendation? Recommendation { get; set; }

    [FormSection("UnitCommander")]
    public string RecommendationRemarks { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string CommanderName { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public MilitaryRank? CommanderRank { get; set; }

    [FormSection("UnitCommander")]
    public string CommanderOrganization { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public DateTime? CommanderSignatureDate { get; set; }

    [FormSection("UnitCommander")]
    public bool OsiReportsReviewed { get; set; }

    [FormSection("UnitCommander")]
    public bool MilitaryPoliceReportsReviewed { get; set; }

    [FormSection("UnitCommander")]
    public string CommanderToLine { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string CommanderFromLine { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public bool WasPresentForDuty { get; set; }

    [FormSection("UnitCommander")]
    public bool WasOnDuty { get; set; }

    [FormSection("UnitCommander")]
    public bool WasOnIDT { get; set; }

    [FormSection("UnitCommander")]
    public bool WasAbsentWithLeave { get; set; }

    [FormSection("UnitCommander")]
    public bool WasAbsentWithoutLeave { get; set; }

    [FormSection("UnitCommander")]
    public string AbsentWithoutLeaveDate1 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string AbsentWithoutLeaveTime1 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string AbsentWithoutLeaveDate2 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string AbsentWithoutLeaveTime2 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string WitnessNameAddress1 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string WitnessNameAddress2 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string WitnessNameAddress3 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string WitnessNameAddress4 { get; set; } = string.Empty;

    [FormSection("UnitCommander")]
    public string WitnessNameAddress5 { get; set; } = string.Empty;

    [FormSection("WingCommander")]
    public bool? IsLegallySufficient { get; set; }

    [FormSection("WingCommander")]
    public bool? ConcurWithRecommendation { get; set; }

    [FormSection("WingCommander")]
    public string NonConcurrenceReason { get; set; } = string.Empty;

    [FormSection("WingCommander")]
    public string LegalRemarks { get; set; } = string.Empty;

    [FormSection("WingCommander")]
    public string SJAName { get; set; } = string.Empty;

    [FormSection("WingCommander")]
    public MilitaryRank? SJARank { get; set; }

    [FormSection("WingCommander")]
    public string SJAOrganization { get; set; } = string.Empty;

    [FormSection("WingCommander")]
    public DateTime? SJASignatureDate { get; set; }

    [FormSection("WingCommander")]
    public string WingCcSignature { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public string PSCDocumentation { get; set; } = string.Empty;

    [FormSection("CaseInfo")]
    public bool EightYearRuleApplies { get; set; }

    [FormSection("CaseInfo")]
    public int YearsOfService { get; set; }

    [FormSection("AppointingAuthority")]
    public string AppointingAuthorityNameRank { get; set; } = string.Empty;

    [FormSection("AppointingAuthority")]
    public string AppointingAuthorityDate { get; set; } = string.Empty;

    [FormSection("AppointingAuthority")]
    public string AppointingAuthoritySignature { get; set; } = string.Empty;

    [FormSection("Board")]
    public string MedicalReviewText { get; set; } = string.Empty;

    [FormSection("Board")]
    public string MedicalReviewerNameRank { get; set; } = string.Empty;

    [FormSection("Board")]
    public string MedicalReviewDate { get; set; } = string.Empty;

    [FormSection("Board")]
    public string MedicalReviewerSignature { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LegalReviewText { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LegalReviewerNameRank { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LegalReviewDate { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LegalReviewerSignature { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LodBoardChairNameRank { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LodBoardChairDate { get; set; } = string.Empty;

    [FormSection("Board")]
    public string LodBoardChairSignature { get; set; } = string.Empty;

    [FormSection("Board")]
    public LineOfDutyFinding? BoardFinding { get; set; }

    [FormSection("Board")]
    public bool BoardReferForFormal { get; set; }

    [FormSection("ApprovingAuthority")]
    public string ApprovingAuthorityNameRank { get; set; } = string.Empty;

    [FormSection("ApprovingAuthority")]
    public string ApprovingAuthorityDate { get; set; } = string.Empty;

    [FormSection("ApprovingAuthority")]
    public string ApprovingAuthoritySignature { get; set; } = string.Empty;

    [FormSection("ApprovingAuthority")]
    public LineOfDutyFinding? ApprovingFinding { get; set; }

    [FormSection("ApprovingAuthority")]
    public bool ApprovingReferForFormal { get; set; }

    public string MemberFullName
    {
        get
        {
            var mi = string.IsNullOrWhiteSpace(MiddleInitial) ? string.Empty : $" {MiddleInitial}.";
            return $"{LastName}, {FirstName}{mi}".Trim(' ', ',');
        }
    }

    public bool ShowArcSection =>
        Component.Contains("Reserve", StringComparison.OrdinalIgnoreCase) ||
        Component.Contains("National Guard", StringComparison.OrdinalIgnoreCase);

    public bool ShowSubstanceType => WasUnderInfluence == true;

    public bool ShowToxicologyResults => ToxicologyTestDone == true;

    public bool ShowPsychEvalDetails => PsychiatricEvalCompleted == true;

    public bool ShowOtherTestDetails => OtherTestsDone == true;

    public bool ShowArcSubFields => IsAtDeployedLocation == false;

    public bool ShowServiceAggravated => IsEptsNsa == true;

    public bool ShowMisconductExplanation => ResultOfMisconduct == true;

    public bool ShowOtherSourceDescription => OtherSourcesReviewed == true;

    public bool ShowNonConcurrenceReason => ConcurWithRecommendation == false;

    public bool ShowInterimLODExpiration => IsInterimLOD;

    public bool ShowAbsentWithoutLeaveDetails => WasAbsentWithoutLeave;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsMilitaryFacility == true && string.IsNullOrWhiteSpace(TreatmentFacilityName))
        {
            yield return new ValidationResult("Facility name is required when treatment was at a military facility.", [nameof(TreatmentFacilityName)]);
        }

        if (WasUnderInfluence == true && SubstanceType == null)
        {
            yield return new ValidationResult("Substance type is required when member was under the influence.", [nameof(SubstanceType)]);
        }

        if (ToxicologyTestDone == true && string.IsNullOrWhiteSpace(ToxicologyTestResults))
        {
            yield return new ValidationResult("Toxicology test results are required when testing was performed.", [nameof(ToxicologyTestResults)]);
        }

        if (PsychiatricEvalCompleted == true)
        {
            if (PsychiatricEvalDate == null)
            {
                yield return new ValidationResult("Psychiatric evaluation date is required.", [nameof(PsychiatricEvalDate)]);
            }

            if (string.IsNullOrWhiteSpace(PsychiatricEvalResults))
            {
                yield return new ValidationResult("Psychiatric evaluation results are required.", [nameof(PsychiatricEvalResults)]);
            }
        }

        if (OtherTestsDone == true)
        {
            if (OtherTestDate == null)
            {
                yield return new ValidationResult("Other test date is required.", [nameof(OtherTestDate)]);
            }

            if (string.IsNullOrWhiteSpace(OtherTestResults))
            {
                yield return new ValidationResult("Other test results are required.", [nameof(OtherTestResults)]);
            }
        }

        if (IsEptsNsa == true && IsServiceAggravated == null)
        {
            yield return new ValidationResult("Service aggravation determination is required for EPTS conditions.", [nameof(IsServiceAggravated)]);
        }

        if (IsAtDeployedLocation == false && RequiresArcBoard == null)
        {
            yield return new ValidationResult("ARC board review determination is required.", [nameof(RequiresArcBoard)]);
        }

        if (TreatmentDateTime > DateTime.Now)
        {
            yield return new ValidationResult("Treatment date cannot be in the future.", [nameof(TreatmentDateTime)]);
        }

        if (PsychiatricEvalDate > DateTime.Now)
        {
            yield return new ValidationResult("Psychiatric evaluation date cannot be in the future.", [nameof(PsychiatricEvalDate)]);
        }

        if (OtherTestDate > DateTime.Now)
        {
            yield return new ValidationResult("Other test date cannot be in the future.", [nameof(OtherTestDate)]);
        }
    }
}
