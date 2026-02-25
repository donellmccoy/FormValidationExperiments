using ECTSystem.Shared.Enums;

namespace ECTSystem.Shared.Models;

/// <summary>
/// Main class representing an LOD case, supporting both informal and formal processes.
/// </summary>
public class LineOfDutyCase : AuditableEntity
{
    public int Id { get; set; }

    // Basic Case Information
    public string CaseId { get; set; } = string.Empty;
    public LineOfDutyProcessType ProcessType { get; set; } // Informal or Formal
    public LineOfDutyWorkflowState WorkflowState { get; set; } = LineOfDutyWorkflowState.MemberInformationEntry;
    public ServiceComponent Component { get; set; } // RegAF, AFR, etc.
    public string MemberName { get; set; } = string.Empty;
    public string MemberRank { get; set; } = string.Empty;
    public string ServiceNumber { get; set; } = string.Empty; // SSN or DoD ID
    public DateTime? MemberDateOfBirth { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string FromLine { get; set; } = string.Empty; // Item 1: "FROM" line on form
    public IncidentType IncidentType { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IncidentDescription { get; set; } = string.Empty;
    public DutyStatus IncidentDutyStatus { get; set; }

    // Part I: Orders / Duty Period
    public string MemberOrdersStartTime { get; set; } = string.Empty; // HHmm format
    public DateTime? MemberOrdersEndDate { get; set; }
    public string MemberOrdersEndTime { get; set; } = string.Empty; // HHmm format

    // Part I Item 2: Type of Medical Unit Reporting
    public bool IsMTF { get; set; }      // Medical Treatment Facility
    public bool IsRMU { get; set; }      // Ready Medical Unit
    public bool IsGMU { get; set; }      // Guard Medical Unit
    public bool IsDeployedLocation { get; set; }

    // Part I Item 8: Additional component types
    public bool IsUSAFA { get; set; }    // USAF Academy
    public bool IsAFROTC { get; set; }   // Air Force ROTC

    // Medical Assessment Fields
    public bool? IsMilitaryFacility { get; set; }
    public string TreatmentFacilityName { get; set; } = string.Empty;
    public DateTime? TreatmentDateTime { get; set; }
    public string ClinicalDiagnosis { get; set; } = string.Empty;
    public string MedicalFindings { get; set; } = string.Empty;
    public bool? WasUnderInfluence { get; set; }
    public SubstanceType? SubstanceType { get; set; }
    public bool? WasMentallyResponsible { get; set; }
    public bool? PsychiatricEvalCompleted { get; set; }
    public DateTime? PsychiatricEvalDate { get; set; }
    public string PsychiatricEvalResults { get; set; } = string.Empty;
    public string OtherRelevantConditions { get; set; } = string.Empty;
    public bool? OtherTestsDone { get; set; }
    public DateTime? OtherTestDate { get; set; }
    public string OtherTestResults { get; set; } = string.Empty;
    public bool? IsServiceAggravated { get; set; }
    public bool? IsPotentiallyUnfitting { get; set; }
    public bool? IsAtDeployedLocation { get; set; }
    public bool? RequiresArcBoard { get; set; }
    public string MedicalRecommendation { get; set; } = string.Empty;

    // Commander Review — Sources of Information
    public bool MemberStatementReviewed { get; set; }
    public bool MedicalRecordsReviewed { get; set; }
    public bool WitnessStatementsReviewed { get; set; }
    public bool PoliceReportsReviewed { get; set; }
    public bool CommanderReportReviewed { get; set; }
    public bool OsiReportsReviewed { get; set; }       // Item 18: OSI
    public bool MilitaryPoliceReportsReviewed { get; set; } // Item 18: Military Police
    public bool OtherSourcesReviewed { get; set; }
    public string OtherSourcesDescription { get; set; } = string.Empty;
    public string MisconductExplanation { get; set; } = string.Empty;
    public string CommanderToLine { get; set; } = string.Empty; // Item 16: "TO" line
    public string CommanderFromLine { get; set; } = string.Empty; // Item 16: "FROM" line

    // Commander Review — Item 19: Duty Status at Time of Incident
    public bool WasPresentForDuty { get; set; }
    public bool WasOnDuty { get; set; }
    public bool WasOnIDT { get; set; }          // Inactive Duty Training
    public bool WasAbsentWithLeave { get; set; }
    public bool WasAbsentWithoutLeave { get; set; }
    public string AbsentWithoutLeaveDate1 { get; set; } = string.Empty;
    public string AbsentWithoutLeaveTime1 { get; set; } = string.Empty;
    public string AbsentWithoutLeaveDate2 { get; set; } = string.Empty;
    public string AbsentWithoutLeaveTime2 { get; set; } = string.Empty;

    // Commander Review — Item 21: Witnesses
    public string WitnessNameAddress1 { get; set; } = string.Empty;
    public string WitnessNameAddress2 { get; set; } = string.Empty;
    public string WitnessNameAddress3 { get; set; } = string.Empty;
    public string WitnessNameAddress4 { get; set; } = string.Empty;
    public string WitnessNameAddress5 { get; set; } = string.Empty;

    // Process Details
    public DateTime InitiationDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int TotalTimelineDays { get; set; } // e.g., 90 for Informal, 160 for Formal
    public bool IsInterimLOD { get; set; }
    public DateTime? InterimLODExpiration { get; set; } // Valid for 90 days
    public List<TimelineStep> TimelineSteps { get; set; } = [];
    public List<LineOfDutyAuthority> Authorities { get; set; } = [];
    public List<WorkflowStepHistory> WorkflowStepHistories { get; set; } = [];

    // Findings and Determinations
    public LineOfDutyFinding FinalFinding { get; set; }
    public string ProximateCause { get; set; } = string.Empty; // For NILOD
    public bool IsPriorServiceCondition { get; set; }
    public string PSCDocumentation { get; set; } = string.Empty;
    public bool EightYearRuleApplies { get; set; }
    public int YearsOfService { get; set; }

    // Signatures and Dates — Part II Provider (Item 15)
    public string ProviderNameRank { get; set; } = string.Empty;
    public string ProviderDate { get; set; } = string.Empty;
    public string ProviderSignature { get; set; } = string.Empty;

    // Signatures and Dates — Part III Commander (Item 23)
    public string CommanderNameRank { get; set; } = string.Empty;
    public string CommanderDate { get; set; } = string.Empty;
    public string CommanderSignature { get; set; } = string.Empty;

    // Part IV: SJA/Legal Review (Items 24–25)
    public string SjaNameRank { get; set; } = string.Empty;
    public string SjaDate { get; set; } = string.Empty;
    public bool SjaConcurs { get; set; }

    // Signatures and Dates — Part V Wing CC / Appointing Authority (Items 25–27)
    public string WingCcSignature { get; set; } = string.Empty;  // Item 25
    public string AppointingAuthorityNameRank { get; set; } = string.Empty; // Item 27
    public string AppointingAuthorityDate { get; set; } = string.Empty;
    public string AppointingAuthoritySignature { get; set; } = string.Empty;

    // Part VI: Formal Board Review (Items 28–33)
    public string MedicalReviewText { get; set; } = string.Empty;
    public string MedicalReviewerNameRank { get; set; } = string.Empty;
    public string MedicalReviewDate { get; set; } = string.Empty;
    public string MedicalReviewerSignature { get; set; } = string.Empty;
    public string LegalReviewText { get; set; } = string.Empty;
    public string LegalReviewerNameRank { get; set; } = string.Empty;
    public string LegalReviewDate { get; set; } = string.Empty;
    public string LegalReviewerSignature { get; set; } = string.Empty;
    public string LodBoardChairNameRank { get; set; } = string.Empty;
    public string LodBoardChairDate { get; set; } = string.Empty;
    public string LodBoardChairSignature { get; set; } = string.Empty;
    public LineOfDutyFinding? BoardFinding { get; set; }  // Item 32
    public bool BoardReferForFormal { get; set; }         // REFER option

    // Part VII: Approving Authority (Items 34–35)
    public string ApprovingAuthorityNameRank { get; set; } = string.Empty;
    public string ApprovingAuthorityDate { get; set; } = string.Empty;
    public string ApprovingAuthoritySignature { get; set; } = string.Empty;
    public LineOfDutyFinding? ApprovingFinding { get; set; } // Item 34
    public bool ApprovingReferForFormal { get; set; }        // REFER option

    // Notification & Reporting
    public bool? NotifiedMedicalUnitTimely { get; set; }
    public bool? SubmittedMedicalDocumentsTimely { get; set; }

    // Special Handling
    public bool? IsSexualAssaultCase { get; set; }
    public bool? RestrictedReporting { get; set; }
    public string SARCCoordination { get; set; } = string.Empty; // Sexual Assault Response Coordinator

    // Documents and Evidence
    public List<LineOfDutyDocument> Documents { get; set; } = [];
    public List<string> WitnessStatements { get; set; } = [];
    public string ToxicologyReport { get; set; } = string.Empty;

    // Appeals
    public List<LineOfDutyAppeal> Appeals { get; set; } = [];

    // Notifications
    public List<Notification> Notifications { get; set; } = [];

    // Member
    public int MemberId { get; set; }
    public Member Member { get; set; }

    // Related Benefits
    public int MEDCONId { get; set; }
    public MEDCONDetail MEDCON { get; set; } = new MEDCONDetail();
    public int INCAPId { get; set; }
    public INCAPDetails INCAP { get; set; } = new INCAPDetails();
    public bool MemberChoseMEDCON { get; set; } // If eligible for both

    // Audit and Notes
    public bool IsAudited { get; set; }
    public List<string> AuditComments { get; set; } = [];
    public string PointOfContact { get; set; } = string.Empty; // e.g., AF/A1PP email
}
