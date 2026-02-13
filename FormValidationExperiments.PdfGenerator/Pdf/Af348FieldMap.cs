using FormValidationExperiments.Shared.Enums;
using FormValidationExperiments.Shared.Models;

namespace FormValidationExperiments.PdfGenerator.Pdf;

/// <summary>
/// Maps LineOfDutyCase properties to AF Form 348 PDF AcroForm field names.
/// Field names discovered by parsing the AF348_06012015_Template.pdf template.
/// </summary>
public static class Af348FieldMap
{
    public static Dictionary<string, Func<LineOfDutyCase, string?>> CreateFieldMappings()
    {
        return new Dictionary<string, Func<LineOfDutyCase, string?>>(StringComparer.OrdinalIgnoreCase)
        {
            // ── PART I: MEMBER INFORMATION (Page 1) ──

            ["form1[0].Page1[0].part1ToCC[0]"] = c => c.Unit,
            ["form1[0].Page1[0].part1ReportDate[0]"] = c => c.InitiationDate.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part1NameFill[0]"] = c => c.MemberName,
            ["form1[0].Page1[0].part1SSNFill[0]"] = c => c.ServiceNumber,
            ["form1[0].Page1[0].part1Rank[0]"] = c => c.MemberRank,
            ["form1[0].Page1[0].part1Organization[0]"] = c => c.Unit,
            ["form1[0].Page1[0].part1check8RegAF[0]"] = c =>
                c.Component == ServiceComponent.RegularAirForce ? "1" : "Off",
            ["form1[0].Page1[0].part1check8AFR[0]"] = c =>
                c.Component == ServiceComponent.AirForceReserve ? "1" : "Off",
            ["form1[0].Page1[0].part1check8ANG[0]"] = c =>
                c.Component == ServiceComponent.AirNationalGuard ? "1" : "Off",
            ["form1[0].Page1[0].part1MbrStartDate[0]"] = c => c.IncidentDate.ToString("ddMMMyyyy"),

            // ── PART II: MILITARY MEDICAL PROVIDER (Page 1) ──

            ["form1[0].Page1[0].part2check9Injury[0]"] = c =>
                c.IncidentType == IncidentType.Injury ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Death[0]"] = c =>
                c.IncidentType == IncidentType.Death ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Illness[0]"] = c =>
                c.IncidentType == IncidentType.Illness ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Disease[0]"] = c =>
                c.IncidentType == IncidentType.Disease ? "1" : "Off",
            ["form1[0].Page1[0].part2check10MilFacility[0]"] = c =>
                c.IsMilitaryFacility == true ? "1" : "Off",
            ["form1[0].Page1[0].part2check10CivFacility[0]"] = c =>
                c.IsMilitaryFacility == false ? "1" : "Off",
            ["form1[0].Page1[0].part2FacilityName[0]"] = c => c.TreatmentFacilityName,
            ["form1[0].Page1[0].part2check10Date[0]"] = c =>
                c.TreatmentDateTime?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2check10Time[0]"] = c =>
                c.TreatmentDateTime?.ToString("HHmm"),
            ["form1[0].Page1[0].part2Description[0]"] = c => c.ClinicalDiagnosis,
            ["form1[0].Page1[0].part2Details[0]"] = c => c.IncidentDescription,

            // Field 13a - Under influence
            ["form1[0].Page1[0].part2Check13aWas[0]"] = c =>
                c.WasUnderInfluence == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aWasNot[0]"] = c =>
                c.WasUnderInfluence == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aAlcohol[0]"] = c =>
                c.SubstanceType is SubstanceType.Alcohol or SubstanceType.Both ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aDrug[0]"] = c =>
                c.SubstanceType is SubstanceType.Drugs or SubstanceType.Both ? "1" : "Off",

            // Field 13b - Test done
            ["form1[0].Page1[0].part2Check13bYes[0]"] = c =>
                c.OtherTestsDone == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bNo[0]"] = c =>
                c.OtherTestsDone == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bResults[0]"] = c => c.ToxicologyReport,

            // Field 13c - Mentally responsible
            ["form1[0].Page1[0].part2Check13cWas[0]"] = c =>
                c.WasMentallyResponsible == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13cWasNot[0]"] = c =>
                c.WasMentallyResponsible == false ? "1" : "Off",

            // Field 13d - Psychiatric evaluation
            ["form1[0].Page1[0].part2Check13dYes[0]"] = c =>
                c.PsychiatricEvalCompleted == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13dNo[0]"] = c =>
                c.PsychiatricEvalCompleted == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13dDate[0]"] = c =>
                c.PsychiatricEvalDate?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2Check13dResults[0]"] = c => c.PsychiatricEvalResults,

            // Field 13e - Other conditions
            ["form1[0].Page1[0].part2Check13eOther[0]"] = c => c.OtherRelevantConditions,

            // Field 13f - Other tests
            ["form1[0].Page1[0].part2Check13fYes[0]"] = c =>
                c.OtherTestsDone == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13fNo[0]"] = c =>
                c.OtherTestsDone == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13fDate[0]"] = c =>
                c.OtherTestDate?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2Check13fResults[0]"] = c => c.OtherTestResults,

            // Field 14 - ARC info
            ["form1[0].Page1[0].part2Check14aYes[0]"] = c =>
                c.IsAtDeployedLocation == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14aNo[0]"] = c =>
                c.IsAtDeployedLocation == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14bYes[0]"] = c =>
                c.IsPriorServiceCondition ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14bNo[0]"] = c =>
                !c.IsPriorServiceCondition ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14cYes[0]"] = c =>
                c.IsServiceAggravated == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14cNo[0]"] = c =>
                c.IsServiceAggravated == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14dYes[0]"] = c =>
                c.IsPotentiallyUnfitting == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14dNo[0]"] = c =>
                c.IsPotentiallyUnfitting == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14eYes[0]"] = c =>
                c.RequiresArcBoard == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check14eNo[0]"] = c =>
                c.RequiresArcBoard == false ? "1" : "Off",

            // ── PART III: IMMEDIATE COMMANDER (Page 2) ──

            ["form1[0].Page2[0].part3From[0]"] = c => c.Unit,

            // Field 18 - Sources of information
            ["form1[0].Page2[0].part3Check18Member[0]"] = c =>
                c.MemberStatementReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18CivPolice[0]"] = c =>
                c.PoliceReportsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18Witness[0]"] = c =>
                c.WitnessStatementsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18Other[0]"] = c =>
                c.OtherSourcesReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18OtherSpecify[0]"] = c => c.OtherSourcesDescription,

            // Field 19 - Member status
            ["form1[0].Page2[0].part3Check19Present[0]"] = c =>
                c.IncidentDutyStatus is DutyStatus.Title10ActiveDuty or DutyStatus.Title32ActiveDuty
                    ? "1" : "Off",

            // Field 20 - Investigation narrative
            ["form1[0].Page2[0].part3InvestigationResult[0]"] = c => c.IncidentDescription,

            // Field 21 - Proximate cause
            ["form1[0].Page2[0].part3Check20Misconduct[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? "1" : "Off",
            ["form1[0].Page2[0].part3Check20Other[0]"] = c =>
                c.FinalFinding != LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? "1" : "Off",
            ["form1[0].Page2[0].part3Check20OtherSpecify[0]"] = c => c.ProximateCause,

            // Field 22 - LOD recommendation
            ["form1[0].Page2[0].part3Check22ILOD[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page2[0].part3Check22NILOD[0]"] = c =>
                c.FinalFinding is LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page2[0].part3Check22FLOD[0]"] = c =>
                c.ProcessType == LineOfDutyProcessType.Formal ? "1" : "Off",

            // ── PART V: APPOINTING AUTHORITY (Page 2) ──

            ["form1[0].Page2[0].part5Check26ILOD[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page2[0].part5Check26NILOD[0]"] = c =>
                c.FinalFinding is LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page2[0].part5Check26FLOD[0]"] = c =>
                c.ProcessType == LineOfDutyProcessType.Formal ? "1" : "Off",

            // ── PART VIII: REMARKS (Page 3) ──

            ["form1[0].Page3[0].part8Remarks[0]"] = c => c.MedicalRecommendation,

            // ── Case Numbers (all pages) ──

            ["form1[0].Page1[0].lodCaseNumberP1[0]"] = c => c.CaseId,
            ["form1[0].Page2[0].lodCaseNumberP2[0]"] = c => c.CaseId,
            ["form1[0].Page3[0].lodCaseNumberP3[0]"] = c => c.CaseId,
        };
    }
}
