namespace AirForceLODSystem;

/// <summary>
/// Enum representing the possible LOD findings.
/// </summary>
public enum LODFinding
{
    InLineOfDuty,                   // ILOD
    NotInLineOfDutyDueToMisconduct, // NILOD - Due to own misconduct
    NotInLineOfDutyNotDueToMisconduct, // NILOD - Not due to misconduct
    ExistingPriorToServiceNotAggravated, // EPTS-NSA
    ExistingPriorToServiceAggravated,    // EPTS-SA
    PriorServiceCondition,          // PSC
    EightYearRuleApplied,           // For EPTS with 8+ years service
    Undetermined
}
