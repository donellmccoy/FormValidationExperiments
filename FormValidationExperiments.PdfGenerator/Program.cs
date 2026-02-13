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
        IncidentType = IncidentType.Injury,
        IncidentDate = new DateTime(2025, 3, 15),
        IncidentDescription = "Member sustained a right knee injury during physical training on base. " +
                              "Member was performing required fitness assessment when knee buckled during the run portion.",
        IncidentDutyStatus = DutyStatus.Title10ActiveDuty,
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
        InitiationDate = new DateTime(2025, 3, 17),
        FinalFinding = LineOfDutyFinding.InLineOfDuty,
        ProximateCause = "Injury occurred during authorized physical training activity.",
        MemberStatementReviewed = true,
        WitnessStatementsReviewed = true,
        PoliceReportsReviewed = false,
        OtherSourcesReviewed = true,
        OtherSourcesDescription = "Medical records, fitness assessment documentation",
        MedicalRecommendation = "Member should be referred for orthopedic evaluation and possible surgical intervention.",
    };
}
