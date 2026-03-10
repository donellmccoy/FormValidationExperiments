using System.Text.RegularExpressions;
using ECTSystem.Shared.Enums;
using ECTSystem.Shared.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.Advanced;
using PdfSharp.Pdf.IO;

namespace ECTSystem.Api.Services;

/// <summary>
/// Fills the AF Form 348 PDF template with data from a <see cref="LineOfDutyCase"/> using
/// the low-level PDF dictionary API. This avoids PDFsharp font resolver issues since we
/// only set field values without rendering glyphs — the PDF viewer renders text at display time.
/// </summary>
public sealed class AF348PdfService
{
    private readonly string _templatePath;

    public AF348PdfService(IWebHostEnvironment env)
    {
        _templatePath = Path.Combine(env.ContentRootPath, "Templates", "AF348_06012015_Template.pdf");
    }

    /// <summary>
    /// Opens the AF Form 348 template, fills AcroForm fields from the case data,
    /// and returns the resulting PDF as a byte array.
    /// </summary>
    public byte[] GenerateFilledForm(LineOfDutyCase lodCase)
    {
        using var document = PdfReader.Open(_templatePath, PdfDocumentOpenMode.Modify);

        // Remove the XFA data stream so the viewer renders AcroForm field values
        // instead of the XFA XML layer (which causes garbled text with "&" artifacts).
        var acroForm = document.Internals.Catalog.Elements.GetDictionary("/AcroForm");
        acroForm?.Elements.Remove("/XFA");

        var fields = CollectFields(document);
        FillFields(fields, lodCase);
        SetAllFieldsReadOnly(fields);

        using var ms = new MemoryStream();
        document.Save(ms, false);
        return ms.ToArray();
    }

    /// <summary>
    /// Walks the AcroForm field hierarchy and returns a dictionary of field-name → PdfDictionary.
    /// Field names are the leaf /T values (e.g., "part1NameFill").
    /// </summary>
    private static Dictionary<string, PdfDictionary> CollectFields(PdfDocument document)
    {
        var result = new Dictionary<string, PdfDictionary>(StringComparer.OrdinalIgnoreCase);

        var acroForm = document.Internals.Catalog.Elements.GetDictionary("/AcroForm");
        if (acroForm is null) return result;

        var fieldsArray = acroForm.Elements.GetArray("/Fields");
        if (fieldsArray is null) return result;

        foreach (var item in fieldsArray)
        {
            var fieldDict = Resolve(item);
            if (fieldDict is not null)
                WalkFields(fieldDict, result);
        }

        return result;
    }

    private static void WalkFields(PdfDictionary dict, Dictionary<string, PdfDictionary> result)
    {
        var kids = dict.Elements.GetArray("/Kids");
        if (kids is not null)
        {
            foreach (var kid in kids)
            {
                var kidDict = Resolve(kid);
                if (kidDict is not null)
                    WalkFields(kidDict, result);
            }
        }
        else
        {
            // Leaf field — has /T (field name) and /FT (field type).
            // XFA-style PDFs append "[0]" array indices to /T values;
            // strip them so lookups use the base name (e.g. "part1NameFill").
            var name = dict.Elements.GetString("/T");
            if (!string.IsNullOrEmpty(name))
            {
                name = Regex.Replace(name, @"\[\d+\]$", string.Empty);
                result[name] = dict;
            }
        }
    }

    private static PdfDictionary Resolve(PdfItem item)
    {
        if (item is PdfReference reference)
            return reference.Value as PdfDictionary;
        return item as PdfDictionary;
    }

    private static void FillFields(Dictionary<string, PdfDictionary> fields, LineOfDutyCase c)
    {
        // ─── Page 1: Part I — Member Info ───
        SetText(fields, "part1ToCC", c.CommanderToLine);
        SetText(fields, "part1From", c.FromLine);
        SetText(fields, "part1ReportDate", c.IncidentDate.ToString("dd MMM yyyy"));
        SetText(fields, "part1NameFill", c.MemberName);
        SetText(fields, "part1SSNFill", c.ServiceNumber);
        SetText(fields, "part1Organization", c.Unit);
        SetText(fields, "part1MbrStartTime", c.MemberOrdersStartTime);
        SetText(fields, "part1MbrStartDate", c.IncidentDate.ToString("dd MMM yyyy"));
        SetText(fields, "part1MbrEndDate", c.MemberOrdersEndDate?.ToString("dd MMM yyyy") ?? string.Empty);
        SetText(fields, "part1MbrEndTime", c.MemberOrdersEndTime);

        // Rank dropdown
        SetText(fields, "part1Rank", c.MemberRank);

        // Item 2: Type of Medical Unit
        SetCheckbox(fields, "part1check2MTF", c.IsMTF);
        SetCheckbox(fields, "part1check2RMU", c.IsRMU);
        SetCheckbox(fields, "part1check2GMU", c.IsGMU);
        SetCheckbox(fields, "part1check2DepLoc", c.IsDeployedLocation);

        // Item 8: Service Component
        SetCheckbox(fields, "part1check8RegAF", c.Component == ServiceComponent.RegularAirForce);
        SetCheckbox(fields, "part1check8AFR", c.Component == ServiceComponent.AirForceReserve);
        SetCheckbox(fields, "part1check8ANG", c.Component == ServiceComponent.AirNationalGuard);
        SetCheckbox(fields, "part1check8AFROTC", c.IsAFROTC);
        SetCheckbox(fields, "part1check8USAFA", c.IsUSAFA);

        // Case number
        SetText(fields, "lodCaseNumberP1", c.CaseId);

        // ─── Page 1: Part II — Medical Assessment ───

        // Item 9: Incident Type
        SetCheckbox(fields, "part2check9Injury", c.IncidentType == IncidentType.Injury);
        SetCheckbox(fields, "part2check9Death", c.IncidentType == IncidentType.Death);
        SetCheckbox(fields, "part2check9Illness", c.IncidentType == IncidentType.Illness);
        SetCheckbox(fields, "part2check9Disease", c.IncidentType == IncidentType.Disease);

        // Item 10: Treatment Facility
        SetCheckbox(fields, "part2check10MilFacility", c.IsMilitaryFacility == true);
        SetCheckbox(fields, "part2check10CivFacility", c.IsMilitaryFacility == false);
        SetText(fields, "part2FacilityName", c.TreatmentFacilityName);
        SetText(fields, "part2check10Date", c.TreatmentDateTime?.ToString("dd MMM yyyy") ?? string.Empty);
        SetText(fields, "part2check10Time", c.TreatmentDateTime?.ToString("HHmm") ?? string.Empty);

        // Item 11/12: Clinical details
        SetText(fields, "part2Details", c.ClinicalDiagnosis);
        SetText(fields, "part2Description", c.MedicalFindings);

        // Item 13a: Substance involvement
        SetCheckbox(fields, "part2Check13aWas", c.WasUnderInfluence == true);
        SetCheckbox(fields, "part2Check13aWasNot", c.WasUnderInfluence == false);
        SetCheckbox(fields, "part2Check13aAlcohol", c.SubstanceType == SubstanceType.Alcohol || c.SubstanceType == SubstanceType.Both);
        SetCheckbox(fields, "part2Check13aDrug", c.SubstanceType == SubstanceType.Drugs || c.SubstanceType == SubstanceType.Both);

        // Item 13b: Toxicology
        SetCheckbox(fields, "part2Check13bYes", c.OtherTestsDone == true);
        SetCheckbox(fields, "part2Check13bNo", c.OtherTestsDone == false);
        SetText(fields, "part2Check13bResults", c.OtherTestResults);
        SetCheckbox(fields, "part2Check13bAlcohol", c.SubstanceType == SubstanceType.Alcohol || c.SubstanceType == SubstanceType.Both);
        SetCheckbox(fields, "part2Check13bDrug", c.SubstanceType == SubstanceType.Drugs || c.SubstanceType == SubstanceType.Both);

        // Item 13c: Mental responsibility
        SetCheckbox(fields, "part2Check13cWas", c.WasMentallyResponsible == true);
        SetCheckbox(fields, "part2Check13cWasNot", c.WasMentallyResponsible == false);

        // Item 13d: Psychiatric eval
        SetCheckbox(fields, "part2Check13dYes", c.PsychiatricEvalCompleted == true);
        SetCheckbox(fields, "part2Check13dNo", c.PsychiatricEvalCompleted == false);
        SetText(fields, "part2Check13dDate", c.PsychiatricEvalDate?.ToString("dd MMM yyyy") ?? string.Empty);
        SetText(fields, "part2Check13dResults", c.PsychiatricEvalResults);

        // Item 13e: Other conditions
        SetText(fields, "part2Check13eOther", c.OtherRelevantConditions);

        // Item 13f: Other tests
        SetCheckbox(fields, "part2Check13fYes", c.OtherTestsDone == true);
        SetCheckbox(fields, "part2Check13fNo", c.OtherTestsDone == false);
        SetText(fields, "part2Check13fDate", c.OtherTestDate?.ToString("dd MMM yyyy") ?? string.Empty);
        SetText(fields, "part2Check13fResults", c.OtherTestResults);

        // Item 14a-e: Medical opinions
        SetCheckbox(fields, "part2Check14aYes", c.IsServiceAggravated == true);
        SetCheckbox(fields, "part2Check14aNo", c.IsServiceAggravated == false);
        SetCheckbox(fields, "part2Check14bYes", c.IsPotentiallyUnfitting == true);
        SetCheckbox(fields, "part2Check14bNo", c.IsPotentiallyUnfitting == false);
        SetCheckbox(fields, "part2Check14cYes", c.IsAtDeployedLocation == true);
        SetCheckbox(fields, "part2Check14cNo", c.IsAtDeployedLocation == false);
        SetCheckbox(fields, "part2Check14dYes", c.RequiresArcBoard == true);
        SetCheckbox(fields, "part2Check14dNo", c.RequiresArcBoard == false);
        SetCheckbox(fields, "part2Check14eYes", c.IsPriorServiceCondition);
        SetCheckbox(fields, "part2Check14eNo", !c.IsPriorServiceCondition);

        // ─── Page 2: Part II (signatures) ───

        // Item 15: Provider signature block
        SetText(fields, "part2ProviderNameRank", c.ProviderNameRank);
        SetText(fields, "part2ProviderDate", c.ProviderDate);
        SetText(fields, "ProviderSignature15", c.ProviderSignature);

        // ─── Page 2: Part III — Commander Review ───

        SetText(fields, "part3To", c.CommanderToLine);
        SetText(fields, "part3From", c.CommanderFromLine);

        // Item 18: Sources of information
        SetCheckbox(fields, "part3Check18Member", c.MemberStatementReviewed);
        SetCheckbox(fields, "part3Check18Witness", c.WitnessStatementsReviewed);
        SetCheckbox(fields, "part3Check18OSI", c.OsiReportsReviewed);
        SetCheckbox(fields, "part3Check18MilPolice", c.MilitaryPoliceReportsReviewed);
        SetCheckbox(fields, "part3Check18CivPolice", c.PoliceReportsReviewed);
        SetCheckbox(fields, "part3Check18Other", c.OtherSourcesReviewed);
        SetText(fields, "part3Check18OtherSpecify", c.OtherSourcesDescription);

        // Item 19: Duty status at time of incident
        SetCheckbox(fields, "part3Check19Present", c.WasPresentForDuty);
        SetCheckbox(fields, "part3Check19Duty", c.WasOnDuty);
        SetCheckbox(fields, "part3Check19IDT", c.WasOnIDT);
        SetCheckbox(fields, "part3Check19AbsentW", c.WasAbsentWithLeave);
        SetCheckbox(fields, "part3Check19AbsentWO", c.WasAbsentWithoutLeave);
        SetText(fields, "part3Check19AbsentWODate", c.AbsentWithoutLeaveDate1);
        SetText(fields, "part3Check19AbsentWOTime", c.AbsentWithoutLeaveTime1);
        SetText(fields, "part3Check19AbsentWO2Date", c.AbsentWithoutLeaveDate2);
        SetText(fields, "part3Check19AbsentWO2Time", c.AbsentWithoutLeaveTime2);

        // Item 20: Misconduct
        SetCheckbox(fields, "part3Check20Misconduct", !string.IsNullOrEmpty(c.MisconductExplanation));
        SetCheckbox(fields, "part3Check20Other", c.OtherSourcesReviewed);
        SetText(fields, "part3Check20OtherSpecify", c.MisconductExplanation);

        // Investigation result
        SetText(fields, "part3InvestigationResult", c.ProximateCause);

        // Item 21: Witnesses
        SetText(fields, "part3NameAddr1", c.WitnessNameAddress1);
        SetText(fields, "part3NameAddr2", c.WitnessNameAddress2);
        SetText(fields, "part3NameAddr3", c.WitnessNameAddress3);
        SetText(fields, "part3NameAddr4", c.WitnessNameAddress4);
        SetText(fields, "part3NameAddr5", c.WitnessNameAddress5);

        // Item 22: Commander recommendation
        SetCheckbox(fields, "part3Check22ILOD", c.FinalFinding == LineOfDutyFinding.InLineOfDuty);
        SetCheckbox(fields, "part3Check22FLOD", false); // Formal LOD — separate flow
        SetCheckbox(fields, "part3Check22NILOD",
            c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ||
            c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct);

        // Item 23: Commander signature block
        SetText(fields, "part3ICNameRank", c.CommanderNameRank);
        SetText(fields, "part3ICDate", c.CommanderDate);
        SetText(fields, "CommanderSignature23", c.CommanderSignature);

        // ─── Page 2: Part IV — SJA/Legal Review ───

        // Item 24: SJA concurrence
        SetCheckbox(fields, "part4Check24Concur", c.SjaConcurs);
        SetCheckbox(fields, "part4Check24NonConcur", !c.SjaConcurs && !string.IsNullOrEmpty(c.SjaNameRank));
        SetText(fields, "part4AdvocateNameRank", c.SjaNameRank);
        SetText(fields, "part4AdvocateDate", c.SjaDate);

        // ─── Page 2: Part V — Wing CC Finding ───

        // Item 25-26: Wing Commander finding
        SetCheckbox(fields, "part5Check26ILOD", c.FinalFinding == LineOfDutyFinding.InLineOfDuty);
        SetCheckbox(fields, "part5Check26FLOD", false);
        SetCheckbox(fields, "part5Check26NILOD",
            c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ||
            c.FinalFinding == LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct);
        SetText(fields, "WingSignature25", c.WingCcSignature);

        // Case number page 2
        SetText(fields, "lodCaseNumberP2", c.CaseId);

        // ─── Page 3: Part V (continued) — Appointing Authority ───

        SetText(fields, "part5AppointingNameRank", c.AppointingAuthorityNameRank);
        SetText(fields, "part5AppointingDate", c.AppointingAuthorityDate);
        SetText(fields, "AppointingSignature27", c.AppointingAuthoritySignature);

        // ─── Page 3: Part VI — Formal Board Review ───

        // Medical review
        SetText(fields, "part6MedicalReview", c.MedicalReviewText);
        SetText(fields, "part6MedicalNameRank", c.MedicalReviewerNameRank);
        SetText(fields, "part6MedicalDate", c.MedicalReviewDate);
        SetText(fields, "MedicalSignature29", c.MedicalReviewerSignature);

        // Legal review
        SetText(fields, "part6LegalReview", c.LegalReviewText);
        SetText(fields, "part6LegalReviewNameRank", c.LegalReviewerNameRank);
        SetText(fields, "part6LegalReviewDate", c.LegalReviewDate);
        SetText(fields, "LegalSignature31", c.LegalReviewerSignature);

        // Item 32: Board finding
        SetCheckbox(fields, "part6Check32ILOD", c.BoardFinding == LineOfDutyFinding.InLineOfDuty);
        SetCheckbox(fields, "part6Check32FLOD", false);
        SetCheckbox(fields, "part6Check32NILOD",
            c.BoardFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ||
            c.BoardFinding == LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct);
        SetCheckbox(fields, "part6Check32REFER", c.BoardReferForFormal);

        SetText(fields, "part6LODNameRank", c.LodBoardChairNameRank);
        SetText(fields, "part6LODDate", c.LodBoardChairDate);
        SetText(fields, "LODSignature33", c.LodBoardChairSignature);

        // ─── Page 3: Part VII — Approving Authority ───

        // Item 34: Approving finding
        SetCheckbox(fields, "part7Check34ILOD", c.ApprovingFinding == LineOfDutyFinding.InLineOfDuty);
        SetCheckbox(fields, "part7Check34FLOD", false);
        SetCheckbox(fields, "part7Check34NILOD",
            c.ApprovingFinding == LineOfDutyFinding.NotInLineOfDutyDueToMisconduct ||
            c.ApprovingFinding == LineOfDutyFinding.NotInLineOfDutyNotDueToMisconduct);
        SetCheckbox(fields, "part7Check34REFER", c.ApprovingReferForFormal);

        SetText(fields, "part7ApprovingNameRank", c.ApprovingAuthorityNameRank);
        SetText(fields, "part7ApprovingDate", c.ApprovingAuthorityDate);
        SetText(fields, "ApprovingSignature35", c.ApprovingAuthoritySignature);

        // Part VIII: Remarks
        SetText(fields, "part8Remarks", string.Join("\n", c.AuditComments ?? []));

        // Case number page 3
        SetText(fields, "lodCaseNumberP3", c.CaseId);
    }

    /// <summary>
    /// Sets a text field value by writing the /V entry in the field dictionary.
    /// Also sets /AP to null to force the viewer to re-render the field appearance.
    /// </summary>
    private static void SetText(Dictionary<string, PdfDictionary> fields, string fieldName, string value)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return;
        if (string.IsNullOrEmpty(value)) return;

        field.Elements.SetString("/V", value);
        // Remove cached appearance so the viewer regenerates it with the new value
        field.Elements.Remove("/AP");
    }

    /// <summary>
    /// Sets a checkbox by writing /V and /AS. AcroForm checkboxes use a PdfName
    /// for the "on" state (typically "/1") and "/Off" for unchecked.
    /// </summary>
    private static void SetCheckbox(Dictionary<string, PdfDictionary> fields, string fieldName, bool isChecked)
    {
        if (!fields.TryGetValue(fieldName, out var field)) return;

        var onValue = GetCheckboxOnValue(field);
        var value = isChecked ? onValue : "/Off";
        field.Elements.SetName("/V", value);
        field.Elements.SetName("/AS", value);
    }

    /// <summary>
    /// Discovers the "on" value for a checkbox by inspecting /AP/N (Normal appearance).
    /// The "on" key is whichever entry is NOT "Off". Defaults to "/1" if not found.
    /// </summary>
    private static string GetCheckboxOnValue(PdfDictionary field)
    {
        var ap = field.Elements.GetDictionary("/AP");
        if (ap is null) return "/1";

        var normal = ap.Elements.GetDictionary("/N");
        if (normal is null) return "/1";

        foreach (var key in normal.Elements.Keys)
        {
            if (!string.Equals(key, "/Off", StringComparison.OrdinalIgnoreCase))
                return key;
        }

        return "/1";
    }

    /// <summary>
    /// Sets the ReadOnly flag (bit 1 of /Ff) on every field so the PDF cannot be edited.
    /// </summary>
    private static void SetAllFieldsReadOnly(Dictionary<string, PdfDictionary> fields)
    {
        foreach (var field in fields.Values)
        {
            var flags = field.Elements.GetInteger("/Ff");
            field.Elements.SetInteger("/Ff", flags | 1); // Bit 1 = ReadOnly
        }
    }
}
