using FormValidationExperiments.Web.Enums;

namespace FormValidationExperiments.Web.Models;

/// <summary>
/// Main class representing an LOD case, supporting both informal and formal processes.
/// </summary>
public class LODCase
{
    // Basic Case Information
    public string CaseId { get; set; }
    public LineOfDutyProcessType ProcessType { get; set; } // Informal or Formal
    public ServiceComponent Component { get; set; } // RegAF, AFR, etc.
    public string MemberName { get; set; }
    public string MemberRank { get; set; }
    public string ServiceNumber { get; set; } // SSN or DoD ID
    public string Unit { get; set; }
    public IncidentType IncidentType { get; set; }
    public DateTime IncidentDate { get; set; }
    public string IncidentDescription { get; set; }
    public DutyStatus IncidentDutyStatus { get; set; }

    // Process Details
    public DateTime InitiationDate { get; set; }
    public DateTime? CompletionDate { get; set; }
    public int TotalTimelineDays { get; set; } // e.g., 90 for Informal, 160 for Formal
    public bool IsInterimLOD { get; set; }
    public DateTime? InterimLODExpiration { get; set; } // Valid for 90 days
    public List<TimelineStep> TimelineSteps { get; set; } = new List<TimelineStep>();
    public List<LODAuthority> Authorities { get; set; } = new List<LODAuthority>();

    // Findings and Determinations
    public LineOfDutyFinding FinalFinding { get; set; }
    public string ProximateCause { get; set; } // For NILOD
    public bool IsPriorServiceCondition { get; set; }
    public string PSCDocumentation { get; set; }
    public bool EightYearRuleApplies { get; set; }
    public int YearsOfService { get; set; }

    // Special Handling
    public bool IsSexualAssaultCase { get; set; }
    public bool RestrictedReporting { get; set; }
    public string SARCCoordination { get; set; } // Sexual Assault Response Coordinator

    // Documents and Evidence
    public List<LODDocument> Documents { get; set; } = new List<LODDocument>();
    public List<string> WitnessStatements { get; set; } = new List<string>();
    public string ToxicologyReport { get; set; }

    // Appeals
    public List<LODAppeal> Appeals { get; set; } = new List<LODAppeal>();

    // Related Benefits
    public MEDCONDetails MEDCON { get; set; } = new MEDCONDetails();
    public INCAPDetails INCAP { get; set; } = new INCAPDetails();
    public bool MemberChoseMEDCON { get; set; } // If eligible for both

    // Audit and Notes
    public bool IsAudited { get; set; }
    public List<string> AuditComments { get; set; } = new List<string>();
    public string PointOfContact { get; set; } // e.g., AF/A1PP email
}
