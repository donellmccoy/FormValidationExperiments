using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;

namespace ECTSystem.PdfGenerator.Pdf;

/// <summary>
/// Maps LineOfDutyCase properties to AF Form 348 PDF AcroForm field names.
/// Field names discovered by parsing the AF348_06012015_Template.pdf template.
/// Covers all 134 data fields (144 total minus 10 print buttons).
/// </summary>
public static class Af348FieldMap
{
    public static Dictionary<string, Func<LineOfDutyCase, string>> CreateFieldMappings()
    {
        return new Dictionary<string, Func<LineOfDutyCase, string>>(StringComparer.OrdinalIgnoreCase)
        {
            // ── PART I: MEMBER INFORMATION (Page 1) ──

            ["form1[0].Page1[0].part1ToCC[0]"] = c => c.Unit,
            ["form1[0].Page1[0].part1From[0]"] = c => c.FromLine,
            ["form1[0].Page1[0].part1ReportDate[0]"] = c => c.InitiationDate.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part1NameFill[0]"] = c => c.MemberName,
            ["form1[0].Page1[0].part1SSNFill[0]"] = c => c.ServiceNumber,
            ["form1[0].Page1[0].part1Rank[0]"] = c => c.MemberRank,
            ["form1[0].Page1[0].part1Organization[0]"] = c => c.Unit,

            // Item 2: Type of medical unit reporting
            ["form1[0].Page1[0].part1check2MTF[0]"] = c => c.IsMTF ? "1" : "Off",
            ["form1[0].Page1[0].part1check2RMU[0]"] = c => c.IsRMU ? "1" : "Off",
            ["form1[0].Page1[0].part1check2GMU[0]"] = c => c.IsGMU ? "1" : "Off",
            ["form1[0].Page1[0].part1check2DepLoc[0]"] = c => c.IsDeployedLocation ? "1" : "Off",

            // Item 8: Service component
            ["form1[0].Page1[0].part1check8RegAF[0]"] = c =>
                c.Component == ServiceComponent.RegularAirForce ? "1" : "Off",
            ["form1[0].Page1[0].part1check8AFR[0]"] = c =>
                c.Component == ServiceComponent.AirForceReserve ? "1" : "Off",
            ["form1[0].Page1[0].part1check8ANG[0]"] = c =>
                c.Component == ServiceComponent.AirNationalGuard ? "1" : "Off",
            ["form1[0].Page1[0].part1check8USAFA[0]"] = c => c.IsUSAFA ? "1" : "Off",
            ["form1[0].Page1[0].part1check8AFROTC[0]"] = c => c.IsAFROTC ? "1" : "Off",

            // Orders/duty period dates and times
            ["form1[0].Page1[0].part1MbrStartDate[0]"] = c => c.IncidentDate.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part1MbrStartTime[0]"] = c => c.MemberOrdersStartTime,
            ["form1[0].Page1[0].part1MbrEndDate[0]"] = c => c.MemberOrdersEndDate?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part1MbrEndTime[0]"] = c => c.MemberOrdersEndTime,

            // ── PART II: MILITARY MEDICAL PROVIDER (Page 1) ──

            // Item 9: Type of incident
            ["form1[0].Page1[0].part2check9Injury[0]"] = c =>
                c.IncidentType == IncidentType.Injury ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Death[0]"] = c =>
                c.IncidentType == IncidentType.Death ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Illness[0]"] = c =>
                c.IncidentType == IncidentType.Illness ? "1" : "Off",
            ["form1[0].Page1[0].part2check9Disease[0]"] = c =>
                c.IncidentType == IncidentType.Disease ? "1" : "Off",

            // Item 10: Treatment facility
            ["form1[0].Page1[0].part2check10MilFacility[0]"] = c =>
                c.IsMilitaryFacility == true ? "1" : "Off",
            ["form1[0].Page1[0].part2check10CivFacility[0]"] = c =>
                c.IsMilitaryFacility == false ? "1" : "Off",
            ["form1[0].Page1[0].part2FacilityName[0]"] = c => c.TreatmentFacilityName,
            ["form1[0].Page1[0].part2check10Date[0]"] = c =>
                c.TreatmentDateTime?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2check10Time[0]"] = c =>
                c.TreatmentDateTime?.ToString("HHmm"),

            // Items 11-12: Diagnosis and details
            ["form1[0].Page1[0].part2Description[0]"] = c => c.ClinicalDiagnosis,
            ["form1[0].Page1[0].part2Details[0]"] = c => c.IncidentDescription,

            // Item 13a: Under influence
            ["form1[0].Page1[0].part2Check13aWas[0]"] = c =>
                c.WasUnderInfluence == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aWasNot[0]"] = c =>
                c.WasUnderInfluence == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aAlcohol[0]"] = c =>
                c.SubstanceType is SubstanceType.Alcohol or SubstanceType.Both ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13aDrug[0]"] = c =>
                c.SubstanceType is SubstanceType.Drugs or SubstanceType.Both ? "1" : "Off",

            // Item 13b: Test done / substance type
            ["form1[0].Page1[0].part2Check13bYes[0]"] = c =>
                c.OtherTestsDone == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bNo[0]"] = c =>
                c.OtherTestsDone == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bAlcohol[0]"] = c =>
                c.SubstanceType is SubstanceType.Alcohol or SubstanceType.Both ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bDrug[0]"] = c =>
                c.SubstanceType is SubstanceType.Drugs or SubstanceType.Both ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13bResults[0]"] = c => c.ToxicologyReport,

            // Item 13c: Mentally responsible
            ["form1[0].Page1[0].part2Check13cWas[0]"] = c =>
                c.WasMentallyResponsible == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13cWasNot[0]"] = c =>
                c.WasMentallyResponsible == false ? "1" : "Off",

            // Item 13d: Psychiatric evaluation
            ["form1[0].Page1[0].part2Check13dYes[0]"] = c =>
                c.PsychiatricEvalCompleted == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13dNo[0]"] = c =>
                c.PsychiatricEvalCompleted == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13dDate[0]"] = c =>
                c.PsychiatricEvalDate?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2Check13dResults[0]"] = c => c.PsychiatricEvalResults,

            // Item 13e: Other conditions
            ["form1[0].Page1[0].part2Check13eOther[0]"] = c => c.OtherRelevantConditions,

            // Item 13f: Other tests
            ["form1[0].Page1[0].part2Check13fYes[0]"] = c =>
                c.OtherTestsDone == true ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13fNo[0]"] = c =>
                c.OtherTestsDone == false ? "1" : "Off",
            ["form1[0].Page1[0].part2Check13fDate[0]"] = c =>
                c.OtherTestDate?.ToString("ddMMMyyyy"),
            ["form1[0].Page1[0].part2Check13fResults[0]"] = c => c.OtherTestResults,

            // Item 14: ARC-specific questions
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

            // Item 16: To/From
            ["form1[0].Page2[0].part3To[0]"] = c => c.CommanderToLine,
            ["form1[0].Page2[0].part3From[0]"] = c =>
                string.IsNullOrEmpty(c.CommanderFromLine) ? c.Unit : c.CommanderFromLine,

            // Item 18: Sources of information
            ["form1[0].Page2[0].part3Check18Member[0]"] = c =>
                c.MemberStatementReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18CivPolice[0]"] = c =>
                c.PoliceReportsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18Witness[0]"] = c =>
                c.WitnessStatementsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18OSI[0]"] = c =>
                c.OsiReportsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18MilPolice[0]"] = c =>
                c.MilitaryPoliceReportsReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18Other[0]"] = c =>
                c.OtherSourcesReviewed ? "1" : "Off",
            ["form1[0].Page2[0].part3Check18OtherSpecify[0]"] = c => c.OtherSourcesDescription,

            // Item 19: Duty status at time of incident
            ["form1[0].Page2[0].part3Check19Present[0]"] = c =>
                c.WasPresentForDuty ? "1" : "Off",
            ["form1[0].Page2[0].part3Check19Duty[0]"] = c =>
                c.WasOnDuty ? "1" : "Off",
            ["form1[0].Page2[0].part3Check19IDT[0]"] = c =>
                c.WasOnIDT ? "1" : "Off",
            ["form1[0].Page2[0].part3Check19AbsentW[0]"] = c =>
                c.WasAbsentWithLeave ? "1" : "Off",
            ["form1[0].Page2[0].part3Check19AbsentWO[0]"] = c =>
                c.WasAbsentWithoutLeave ? "1" : "Off",
            ["form1[0].Page2[0].part3Check19AbsentWODate[0]"] = c => c.AbsentWithoutLeaveDate1,
            ["form1[0].Page2[0].part3Check19AbsentWOTime[0]"] = c => c.AbsentWithoutLeaveTime1,
            ["form1[0].Page2[0].part3Check19AbsentWO2Date[0]"] = c => c.AbsentWithoutLeaveDate2,
            ["form1[0].Page2[0].part3Check19AbsentWO2Time[0]"] = c => c.AbsentWithoutLeaveTime2,

            // Item 20: Investigation narrative
            ["form1[0].Page2[0].part3InvestigationResult[0]"] = c => c.IncidentDescription,

            // Item 21: Proximate cause
            ["form1[0].Page2[0].part3Check20Misconduct[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? "1" : "Off",
            ["form1[0].Page2[0].part3Check20Other[0]"] = c =>
                c.FinalFinding != LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ? "1" : "Off",
            ["form1[0].Page2[0].part3Check20OtherSpecify[0]"] = c => c.ProximateCause,

            // Item 21: Witness names and addresses
            ["form1[0].Page2[0].part3NameAddr1[0]"] = c => c.WitnessNameAddress1,
            ["form1[0].Page2[0].part3NameAddr2[0]"] = c => c.WitnessNameAddress2,
            ["form1[0].Page2[0].part3NameAddr3[0]"] = c => c.WitnessNameAddress3,
            ["form1[0].Page2[0].part3NameAddr4[0]"] = c => c.WitnessNameAddress4,
            ["form1[0].Page2[0].part3NameAddr5[0]"] = c => c.WitnessNameAddress5,

            // Item 22: Commander's LOD recommendation
            ["form1[0].Page2[0].part3Check22ILOD[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page2[0].part3Check22NILOD[0]"] = c =>
                c.FinalFinding is LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page2[0].part3Check22FLOD[0]"] = c =>
                c.ProcessType == LineOfDutyProcessType.Formal ? "1" : "Off",

            // Part II Provider signature block (Item 15, Page 2)
            ["form1[0].Page2[0].part2ProviderNameRank[0]"] = c => c.ProviderNameRank,
            ["form1[0].Page2[0].part2ProviderDate[0]"] = c => c.ProviderDate,
            ["form1[0].Page2[0].ProviderSignature15[0]"] = c => c.ProviderSignature,

            // Part III Commander signature block (Item 23, Page 2)
            ["form1[0].Page2[0].part3ICNameRank[0]"] = c => c.CommanderNameRank,
            ["form1[0].Page2[0].part3ICDate[0]"] = c => c.CommanderDate,
            ["form1[0].Page2[0].CommanderSignature23[0]"] = c => c.CommanderSignature,

            // ── PART IV: SJA/LEGAL ADVISOR REVIEW (Page 2) ──

            ["form1[0].Page2[0].part4Check24Concur[0]"] = c =>
                c.SjaConcurs ? "1" : "Off",
            ["form1[0].Page2[0].part4Check24NonConcur[0]"] = c =>
                !c.SjaConcurs ? "1" : "Off",
            ["form1[0].Page2[0].part4AdvocateNameRank[0]"] = c => c.SjaNameRank,
            ["form1[0].Page2[0].part4AdvocateDate[0]"] = c => c.SjaDate,

            // ── PART V: APPOINTING AUTHORITY (Page 2) ──

            ["form1[0].Page2[0].part5Check26ILOD[0]"] = c =>
                c.FinalFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page2[0].part5Check26NILOD[0]"] = c =>
                c.FinalFinding is LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page2[0].part5Check26FLOD[0]"] = c =>
                c.ProcessType == LineOfDutyProcessType.Formal ? "1" : "Off",
            ["form1[0].Page2[0].WingSignature25[0]"] = c => c.WingCcSignature,

            // ── PART V: APPOINTING AUTHORITY (Page 3) ──

            ["form1[0].Page3[0].part5AppointingNameRank[0]"] = c => c.AppointingAuthorityNameRank,
            ["form1[0].Page3[0].part5AppointingDate[0]"] = c => c.AppointingAuthorityDate,
            ["form1[0].Page3[0].AppointingSignature27[0]"] = c => c.AppointingAuthoritySignature,

            // ── PART VI: FORMAL BOARD REVIEW (Page 3) ──

            // Medical reviewer (Items 28-29)
            ["form1[0].Page3[0].part6MedicalReview[0]"] = c => c.MedicalReviewText,
            ["form1[0].Page3[0].part6MedicalNameRank[0]"] = c => c.MedicalReviewerNameRank,
            ["form1[0].Page3[0].part6MedicalDate[0]"] = c => c.MedicalReviewDate,
            ["form1[0].Page3[0].MedicalSignature29[0]"] = c => c.MedicalReviewerSignature,

            // Legal reviewer (Items 30-31)
            ["form1[0].Page3[0].part6LegalReview[0]"] = c => c.LegalReviewText,
            ["form1[0].Page3[0].part6LegalReviewNameRank[0]"] = c => c.LegalReviewerNameRank,
            ["form1[0].Page3[0].part6LegalReviewDate[0]"] = c => c.LegalReviewDate,
            ["form1[0].Page3[0].LegalSignature31[0]"] = c => c.LegalReviewerSignature,

            // LOD Board Chair (Items 32-33)
            ["form1[0].Page3[0].part6LODNameRank[0]"] = c => c.LodBoardChairNameRank,
            ["form1[0].Page3[0].part6LODDate[0]"] = c => c.LodBoardChairDate,
            ["form1[0].Page3[0].LODSignature33[0]"] = c => c.LodBoardChairSignature,

            // Item 32: Board finding determination
            ["form1[0].Page3[0].part6Check32ILOD[0]"] = c =>
                c.BoardFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page3[0].part6Check32NILOD[0]"] = c =>
                c.BoardFinding is LineOfDutyFinding.NotInLineOfDutyDueToMisconduct
                    or LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page3[0].part6Check32FLOD[0]"] = c =>
                c.BoardFinding == LineOfDutyFinding.Undetermined ? "1" : "Off",
            ["form1[0].Page3[0].part6Check32REFER[0]"] = c =>
                c.BoardReferForFormal ? "1" : "Off",

            // ── PART VII: APPROVING AUTHORITY (Page 3) ──

            // Items 34-35
            ["form1[0].Page3[0].part7ApprovingNameRank[0]"] = c => c.ApprovingAuthorityNameRank,
            ["form1[0].Page3[0].part7ApprovingDate[0]"] = c => c.ApprovingAuthorityDate,
            ["form1[0].Page3[0].ApprovingSignature35[0]"] = c => c.ApprovingAuthoritySignature,

            // Item 34: Approving authority finding
            ["form1[0].Page3[0].part7Check34ILOD[0]"] = c =>
                c.ApprovingFinding == LineOfDutyFinding.InLineOfDuty ? "1" : "Off",
            ["form1[0].Page3[0].part7Check34NILOD[0]"] = c =>
                c.ApprovingFinding is LineOfDutyFinding.NotInLineOfDutyDueToMisconduct
                    or LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct
                    or LineOfDutyFinding.ExistingPriorToServiceNotAggravated ? "1" : "Off",
            ["form1[0].Page3[0].part7Check34FLOD[0]"] = c =>
                c.ApprovingFinding == LineOfDutyFinding.Undetermined ? "1" : "Off",
            ["form1[0].Page3[0].part7Check34REFER[0]"] = c =>
                c.ApprovingReferForFormal ? "1" : "Off",

            // ── PART VIII: REMARKS (Page 3) ──

            ["form1[0].Page3[0].part8Remarks[0]"] = c => c.MedicalRecommendation,

            // ── Case Numbers (all pages) ──

            ["form1[0].Page1[0].lodCaseNumberP1[0]"] = c => c.CaseId,
            ["form1[0].Page2[0].lodCaseNumberP2[0]"] = c => c.CaseId,
            ["form1[0].Page3[0].lodCaseNumberP3[0]"] = c => c.CaseId,
        };
    }
}
