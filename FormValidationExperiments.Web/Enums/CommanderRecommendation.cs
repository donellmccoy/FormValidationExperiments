namespace FormValidationExperiments.Web.Enums;

/// <summary>
/// Enum representing the commander's LOD recommendation (AF Form 348, Item 21).
/// </summary>
public enum CommanderRecommendation
{
    InLineOfDuty,
    NotInLineOfDutyDueToMisconduct,
    NotInLineOfDutyNotDueToMisconduct,
    ReferToFormalInvestigation
}
