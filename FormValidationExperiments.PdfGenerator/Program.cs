using FormValidationExperiments.PdfGenerator.Pdf;
using FormValidationExperiments.PdfGenerator.Pdf.AcroForm;
using FormValidationExperiments.PdfGenerator.Pdf.Parsing;
using FormValidationExperiments.Shared.Enums;
using FormValidationExperiments.Shared.Models;

var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "AF348_06012015_Template.pdf");

if (!File.Exists(templatePath))
{
    Console.Error.WriteLine($"Template not found at: {templatePath}");
    return 1;
}

args = new string[2];
args[0] = "--fill";
args[1] = "c:\\AF348_Filled.pdf";

var templateBytes = File.ReadAllBytes(templatePath);
var mode = args.Length > 0 ? args[0] : "--list-fields";

switch (mode)
{
    case "--list-fields":
        ListFields(templateBytes);
        break;

    case "--fill":
        var outputPath = args.Length > 1 ? args[1] : "AF348_Filled.pdf";
        FillWithSampleData(templateBytes, outputPath);
        break;

    default:
        Console.WriteLine("Usage:");
        Console.WriteLine("  --list-fields              List all AcroForm field names in the template");
        Console.WriteLine("  --fill [output.pdf]        Fill template with sample data and save");
        break;
}

return 0;

static void ListFields(byte[] templateBytes)
{
    Console.WriteLine("Parsing PDF template...");
    Console.WriteLine();

    var parser = new PdfParser(templateBytes);
    var reader = new AcroFormReader(parser);
    var fields = reader.ReadFields();

    Console.WriteLine($"Found {fields.Count} form fields:");
    Console.WriteLine();
    Console.WriteLine($"{"#",-4} {"Type",-10} {"Current Value",-20} {"On Value",-10} Field Name");
    Console.WriteLine(new string('-', 100));

    var i = 1;
    foreach (var field in fields)
    {
        Console.WriteLine(
            $"{i,-4} {field.FieldType,-10} {(field.CurrentValue ?? "(empty)"),-20} {(field.OnValue ?? ""),-10} {field.FullyQualifiedName}");
        i++;
    }
}

static void FillWithSampleData(byte[] templateBytes, string outputPath)
{
    Console.WriteLine("Parsing PDF template...");

    // First list the fields so the user can see what's available
    var parser = new PdfParser(templateBytes);
    var reader = new AcroFormReader(parser);
    var fields = reader.ReadFields();
    Console.WriteLine($"Found {fields.Count} form fields in template.");

    // Create sample case data
    var sampleCase = CreateSampleCase();

    // Build field values from the mapping
    var fieldMappings = Af348FieldMap.CreateFieldMappings();
    var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    foreach (var (pdfFieldName, valueExtractor) in fieldMappings)
    {
        var value = valueExtractor(sampleCase);
        if (value is not null)
            fieldValues[pdfFieldName] = value;
    }

    Console.WriteLine($"Mapped {fieldValues.Count} field values from sample case.");

    // Fill the PDF
    var writer = new AcroFormWriter(templateBytes);
    var filledPdf = writer.FillFields(fieldValues);

    File.WriteAllBytes(outputPath, filledPdf);
    Console.WriteLine($"Filled PDF written to: {Path.GetFullPath(outputPath)}");
    Console.WriteLine($"Output size: {filledPdf.Length:N0} bytes");
}

static LineOfDutyCase CreateSampleCase()
{
    return new LineOfDutyCase
    {
        Id = 1,
        CaseId = "LOD-2025-00001",
        ProcessType = LineOfDutyProcessType.Informal,
        Component = ServiceComponent.AirForceReserve,
        MemberName = "Doe, John E.",
        MemberRank = "TSgt/E-6",
        ServiceNumber = "123-45-6789",
        Unit = "944 ASTS",
        FromLine = "944 ASTS/SGPE",
        IncidentType = IncidentType.Injury,
        IncidentDate = new DateTime(2025, 3, 15),
        IncidentDescription = "Member sustained a right knee injury during physical training on base. " +
                              "Member was performing required fitness assessment when knee buckled during the run portion.",
        IncidentDutyStatus = DutyStatus.Title10ActiveDuty,

        // Part I: Orders / Duty Period
        MemberOrdersStartTime = "0730",
        MemberOrdersEndDate = new DateTime(2025, 3, 16),
        MemberOrdersEndTime = "1630",

        // Part I Item 2: Type of Medical Unit Reporting
        IsMTF = true,
        IsRMU = false,
        IsGMU = false,
        IsDeployedLocation = false,

        // Part I Item 8: Additional component types
        IsUSAFA = false,
        IsAFROTC = false,

        // Medical Assessment Fields
        IsMilitaryFacility = true,
        TreatmentFacilityName = "944th Fighter Wing Medical Clinic, Luke AFB, AZ",
        TreatmentDateTime = new DateTime(2025, 3, 15, 14, 30, 0),
        ClinicalDiagnosis = "Right knee medial meniscus tear, acute. MRI confirmed Grade II tear.",
        MedicalFindings = "Swelling and tenderness of right knee, limited range of motion.",
        WasUnderInfluence = false,
        WasMentallyResponsible = true,
        PsychiatricEvalCompleted = false,
        OtherTestsDone = true,
        OtherTestDate = new DateTime(2025, 3, 16),
        OtherTestResults = "MRI right knee: Grade II medial meniscus tear confirmed.",
        IsAtDeployedLocation = false,
        IsPriorServiceCondition = false,
        IsServiceAggravated = false,
        IsPotentiallyUnfitting = true,
        RequiresArcBoard = false,

        // Commander Review — Sources of Information
        MemberStatementReviewed = true,
        WitnessStatementsReviewed = true,
        PoliceReportsReviewed = false,
        OsiReportsReviewed = false,
        MilitaryPoliceReportsReviewed = false,
        OtherSourcesReviewed = true,
        OtherSourcesDescription = "Medical records, fitness assessment documentation",
        CommanderToLine = "AFPC/DPFDD",
        CommanderFromLine = "944 ASTS/CC",

        // Commander Review — Item 19: Duty Status
        WasPresentForDuty = true,
        WasOnDuty = true,
        WasOnIDT = false,
        WasAbsentWithLeave = false,
        WasAbsentWithoutLeave = false,
        AbsentWithoutLeaveDate1 = "",
        AbsentWithoutLeaveTime1 = "",
        AbsentWithoutLeaveDate2 = "",
        AbsentWithoutLeaveTime2 = "",

        // Commander Review — Item 21: Witnesses
        WitnessNameAddress1 = "SSgt Jane Smith, 944 ASTS, Luke AFB AZ 85309",
        WitnessNameAddress2 = "SrA Michael Brown, 944 FW/FTN, Luke AFB AZ 85309",
        WitnessNameAddress3 = "Mr. Robert Jones, GS-11, 944 FSS, Luke AFB AZ 85309",
        WitnessNameAddress4 = "",
        WitnessNameAddress5 = "",

        // Process Details
        InitiationDate = new DateTime(2025, 3, 17),
        FinalFinding = LineOfDutyFinding.InLineOfDuty,
        ProximateCause = "Injury occurred during authorized physical training activity.",
        MedicalRecommendation = "Member should be referred for orthopedic evaluation and possible surgical intervention.",

        // Part II Provider Signature (Item 15)
        ProviderNameRank = "Maj Sarah Williams, MC",
        ProviderDate = "15Mar2025",
        ProviderSignature = "WILLIAMS.SARAH.M.1234567890",

        // Part III Commander Signature (Item 23)
        CommanderNameRank = "Lt Col David Martinez",
        CommanderDate = "18Mar2025",
        CommanderSignature = "MARTINEZ.DAVID.A.0987654321",

        // Part IV: SJA/Legal Review (Items 24–25)
        SjaNameRank = "Maj Thomas Anderson, JAGC",
        SjaDate = "19Mar2025",
        SjaConcurs = true,

        // Part V: Wing CC / Appointing Authority (Items 25–27)
        WingCcSignature = "JOHNSON.MARK.R.1122334455",
        AppointingAuthorityNameRank = "Col Mark Johnson",
        AppointingAuthorityDate = "20Mar2025",
        AppointingAuthoritySignature = "JOHNSON.MARK.R.1122334455",

        // Part VI: Formal Board Review (Items 28–33)
        MedicalReviewText = "Medical records reviewed. Injury is consistent with reported mechanism of injury during authorized PT.",
        MedicalReviewerNameRank = "Col Patricia Lee, MC",
        MedicalReviewDate = "25Mar2025",
        MedicalReviewerSignature = "LEE.PATRICIA.A.5566778899",
        LegalReviewText = "Case is legally sufficient. All required documentation present. Recommend ILOD finding.",
        LegalReviewerNameRank = "Lt Col James Wilson, JAGC",
        LegalReviewDate = "26Mar2025",
        LegalReviewerSignature = "WILSON.JAMES.B.6677889900",
        LodBoardChairNameRank = "Col Robert Taylor",
        LodBoardChairDate = "28Mar2025",
        LodBoardChairSignature = "TAYLOR.ROBERT.C.7788990011",
        BoardFinding = LineOfDutyFinding.InLineOfDuty,
        BoardReferForFormal = false,

        // Part VII: Approving Authority (Items 34–35)
        ApprovingAuthorityNameRank = "Brig Gen Susan Clark",
        ApprovingAuthorityDate = "01Apr2025",
        ApprovingAuthoritySignature = "CLARK.SUSAN.D.8899001122",
        ApprovingFinding = LineOfDutyFinding.InLineOfDuty,
        ApprovingReferForFormal = false,
    };
}
