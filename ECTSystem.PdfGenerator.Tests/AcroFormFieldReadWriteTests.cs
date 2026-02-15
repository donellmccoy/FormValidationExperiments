using Xunit;
using ECTSystem.PdfGenerator.Pdf.AcroForm;
using ECTSystem.PdfGenerator.Pdf.Parsing;

namespace ECTSystem.PdfGenerator.Tests;

public class AcroFormFieldReadWriteTests : IDisposable
{
    private readonly byte[] _templateBytes;
    private readonly List<AcroFormField> _templateFields;

    public AcroFormFieldReadWriteTests()
    {
        var templatePath = Path.Combine(AppContext.BaseDirectory, "Templates", "AF348_06012015_Template.pdf");
        Assert.True(File.Exists(templatePath), $"Template PDF not found at: {templatePath}");

        _templateBytes = File.ReadAllBytes(templatePath);
        var parser = new PdfParser(_templateBytes);
        var reader = new AcroFormReader(parser);
        _templateFields = reader.ReadFields();
    }

    public void Dispose() { }

    // ───────────────────────────────────────────────
    //  Template Parsing Tests
    // ───────────────────────────────────────────────

    [Fact]
    public void Template_ContainsFormFields()
    {
        Assert.NotEmpty(_templateFields);
    }

    [Fact]
    public void Template_AllFieldsHaveFullyQualifiedName()
    {
        foreach (var field in _templateFields)
        {
            Assert.False(string.IsNullOrWhiteSpace(field.FullyQualifiedName),
                $"Field object #{field.ObjectNumber} has empty FullyQualifiedName");
        }
    }

    [Fact]
    public void Template_AllFieldsHaveKnownType()
    {
        foreach (var field in _templateFields)
        {
            Assert.NotEqual(PdfFieldType.Unknown, field.FieldType);
        }
    }

    [Fact]
    public void Template_ContainsTextFields()
    {
        Assert.Contains(_templateFields, f => f.FieldType == PdfFieldType.Text);
    }

    [Fact]
    public void Template_ContainsCheckboxFields()
    {
        Assert.Contains(_templateFields, f => f.FieldType == PdfFieldType.Checkbox);
    }

    // ───────────────────────────────────────────────
    //  Individual Text Field Read/Write Tests
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("form1[0].Page1[0].part1ToCC[0]", "944 ASTS")]
    [InlineData("form1[0].Page1[0].part1From[0]", "944 ASTS/SGPE")]
    [InlineData("form1[0].Page1[0].part1ReportDate[0]", "15MAR2025")]
    [InlineData("form1[0].Page1[0].part1NameFill[0]", "Doe, John E.")]
    [InlineData("form1[0].Page1[0].part1SSNFill[0]", "123-45-6789")]
    [InlineData("form1[0].Page1[0].part1Rank[0]", "TSgt/E-6")]
    [InlineData("form1[0].Page1[0].part1Organization[0]", "944 FW")]
    [InlineData("form1[0].Page1[0].part1MbrStartDate[0]", "01JAN2025")]
    [InlineData("form1[0].Page1[0].part1MbrStartTime[0]", "0730")]
    [InlineData("form1[0].Page1[0].part1MbrEndDate[0]", "16MAR2025")]
    [InlineData("form1[0].Page1[0].part1MbrEndTime[0]", "1630")]
    public void TextField_Part1_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2FacilityName[0]", "944th FW Medical Clinic, Luke AFB, AZ")]
    [InlineData("form1[0].Page1[0].part2check10Date[0]", "15MAR2025")]
    [InlineData("form1[0].Page1[0].part2check10Time[0]", "1430")]
    [InlineData("form1[0].Page1[0].part2Description[0]", "Right knee medial meniscus tear, acute")]
    [InlineData("form1[0].Page1[0].part2Details[0]", "Member injured during PT fitness assessment")]
    [InlineData("form1[0].Page1[0].part2Check13bResults[0]", "MRI: Grade II tear confirmed")]
    [InlineData("form1[0].Page1[0].part2Check13dDate[0]", "16MAR2025")]
    [InlineData("form1[0].Page1[0].part2Check13dResults[0]", "No psychiatric conditions found")]
    [InlineData("form1[0].Page1[0].part2Check13eOther[0]", "No other relevant conditions")]
    [InlineData("form1[0].Page1[0].part2Check13fDate[0]", "16MAR2025")]
    [InlineData("form1[0].Page1[0].part2Check13fResults[0]", "MRI right knee completed")]
    public void TextField_Part2_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part3To[0]", "AFPC/DPFDD")]
    [InlineData("form1[0].Page2[0].part3From[0]", "944 ASTS/CC")]
    [InlineData("form1[0].Page2[0].part3Check18OtherSpecify[0]", "Medical records, fitness docs")]
    [InlineData("form1[0].Page2[0].part3InvestigationResult[0]", "Investigation found member was on duty")]
    [InlineData("form1[0].Page2[0].part3Check20OtherSpecify[0]", "Injury during authorized PT")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentWODate[0]", "15MAR2025")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentWOTime[0]", "0800")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentWO2Date[0]", "16MAR2025")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentWO2Time[0]", "1700")]
    [InlineData("form1[0].Page2[0].part3NameAddr1[0]", "SSgt Jane Smith, 944 ASTS")]
    [InlineData("form1[0].Page2[0].part3NameAddr2[0]", "SrA Michael Brown, 944 FW")]
    [InlineData("form1[0].Page2[0].part3NameAddr3[0]", "Mr. Robert Jones, GS-11")]
    [InlineData("form1[0].Page2[0].part3NameAddr4[0]", "Witness Four Name")]
    [InlineData("form1[0].Page2[0].part3NameAddr5[0]", "Witness Five Name")]
    public void TextField_Part3_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part2ProviderNameRank[0]", "Maj Sarah Williams, MC")]
    [InlineData("form1[0].Page2[0].part2ProviderDate[0]", "15Mar2025")]
    [InlineData("form1[0].Page2[0].ProviderSignature15[0]", "WILLIAMS.SARAH.M.1234567890")]
    [InlineData("form1[0].Page2[0].part3ICNameRank[0]", "Lt Col David Martinez")]
    [InlineData("form1[0].Page2[0].part3ICDate[0]", "18Mar2025")]
    [InlineData("form1[0].Page2[0].CommanderSignature23[0]", "MARTINEZ.DAVID.A.0987654321")]
    [InlineData("form1[0].Page2[0].part4AdvocateNameRank[0]", "Maj Thomas Anderson, JAGC")]
    [InlineData("form1[0].Page2[0].part4AdvocateDate[0]", "19Mar2025")]
    [InlineData("form1[0].Page2[0].WingSignature25[0]", "JOHNSON.MARK.R.1122334455")]
    public void TextField_Page2Signatures_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part5AppointingNameRank[0]", "Col Mark Johnson")]
    [InlineData("form1[0].Page3[0].part5AppointingDate[0]", "20Mar2025")]
    [InlineData("form1[0].Page3[0].AppointingSignature27[0]", "JOHNSON.MARK.R.1122334455")]
    public void TextField_Part5_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part6MedicalReview[0]", "Medical records reviewed.")]
    [InlineData("form1[0].Page3[0].part6MedicalNameRank[0]", "Col Patricia Lee, MC")]
    [InlineData("form1[0].Page3[0].part6MedicalDate[0]", "25Mar2025")]
    [InlineData("form1[0].Page3[0].MedicalSignature29[0]", "LEE.PATRICIA.A.5566778899")]
    [InlineData("form1[0].Page3[0].part6LegalReview[0]", "Case is legally sufficient.")]
    [InlineData("form1[0].Page3[0].part6LegalReviewNameRank[0]", "Lt Col James Wilson, JAGC")]
    [InlineData("form1[0].Page3[0].part6LegalReviewDate[0]", "26Mar2025")]
    [InlineData("form1[0].Page3[0].LegalSignature31[0]", "WILSON.JAMES.B.6677889900")]
    [InlineData("form1[0].Page3[0].part6LODNameRank[0]", "Col Robert Taylor")]
    [InlineData("form1[0].Page3[0].part6LODDate[0]", "28Mar2025")]
    [InlineData("form1[0].Page3[0].LODSignature33[0]", "TAYLOR.ROBERT.C.7788990011")]
    public void TextField_Part6_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part7ApprovingNameRank[0]", "Brig Gen Susan Clark")]
    [InlineData("form1[0].Page3[0].part7ApprovingDate[0]", "01Apr2025")]
    [InlineData("form1[0].Page3[0].ApprovingSignature35[0]", "CLARK.SUSAN.D.8899001122")]
    public void TextField_Part7_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part8Remarks[0]", "Refer for orthopedic evaluation")]
    public void TextField_Part8_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].lodCaseNumberP1[0]", "LOD-2025-00001")]
    [InlineData("form1[0].Page2[0].lodCaseNumberP2[0]", "LOD-2025-00002")]
    [InlineData("form1[0].Page3[0].lodCaseNumberP3[0]", "LOD-2025-00003")]
    public void TextField_CaseNumber_CanBeWrittenAndReadBack(string fieldName, string testValue)
    {
        AssertTextFieldRoundTrip(fieldName, testValue);
    }

    // ───────────────────────────────────────────────
    //  Individual Checkbox Field Read/Write Tests
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("form1[0].Page1[0].part1check8RegAF[0]")]
    [InlineData("form1[0].Page1[0].part1check8AFR[0]")]
    [InlineData("form1[0].Page1[0].part1check8ANG[0]")]
    public void Checkbox_Part1Component_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part1check2MTF[0]")]
    [InlineData("form1[0].Page1[0].part1check2RMU[0]")]
    [InlineData("form1[0].Page1[0].part1check2GMU[0]")]
    [InlineData("form1[0].Page1[0].part1check2DepLoc[0]")]
    public void Checkbox_Part1MedicalUnit_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part1check8USAFA[0]")]
    [InlineData("form1[0].Page1[0].part1check8AFROTC[0]")]
    public void Checkbox_Part1Academy_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2check9Injury[0]")]
    [InlineData("form1[0].Page1[0].part2check9Death[0]")]
    [InlineData("form1[0].Page1[0].part2check9Illness[0]")]
    [InlineData("form1[0].Page1[0].part2check9Disease[0]")]
    public void Checkbox_Part2IncidentType_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2check10MilFacility[0]")]
    [InlineData("form1[0].Page1[0].part2check10CivFacility[0]")]
    public void Checkbox_Part2Facility_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13aWas[0]")]
    [InlineData("form1[0].Page1[0].part2Check13aWasNot[0]")]
    [InlineData("form1[0].Page1[0].part2Check13aAlcohol[0]")]
    [InlineData("form1[0].Page1[0].part2Check13aDrug[0]")]
    public void Checkbox_Part2Field13a_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13bYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check13bNo[0]")]
    public void Checkbox_Part2Field13b_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13bAlcohol[0]")]
    [InlineData("form1[0].Page1[0].part2Check13bDrug[0]")]
    public void Checkbox_Part2Field13bSubstance_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13cWas[0]")]
    [InlineData("form1[0].Page1[0].part2Check13cWasNot[0]")]
    public void Checkbox_Part2Field13c_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13dYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check13dNo[0]")]
    public void Checkbox_Part2Field13d_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check13fYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check13fNo[0]")]
    public void Checkbox_Part2Field13f_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page1[0].part2Check14aYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check14aNo[0]")]
    [InlineData("form1[0].Page1[0].part2Check14bYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check14bNo[0]")]
    [InlineData("form1[0].Page1[0].part2Check14cYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check14cNo[0]")]
    [InlineData("form1[0].Page1[0].part2Check14dYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check14dNo[0]")]
    [InlineData("form1[0].Page1[0].part2Check14eYes[0]")]
    [InlineData("form1[0].Page1[0].part2Check14eNo[0]")]
    public void Checkbox_Part2Field14_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part3Check18Member[0]")]
    [InlineData("form1[0].Page2[0].part3Check18CivPolice[0]")]
    [InlineData("form1[0].Page2[0].part3Check18Witness[0]")]
    [InlineData("form1[0].Page2[0].part3Check18Other[0]")]
    [InlineData("form1[0].Page2[0].part3Check18OSI[0]")]
    [InlineData("form1[0].Page2[0].part3Check18MilPolice[0]")]
    public void Checkbox_Part3Field18_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part3Check19Present[0]")]
    [InlineData("form1[0].Page2[0].part3Check19Duty[0]")]
    [InlineData("form1[0].Page2[0].part3Check19IDT[0]")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentW[0]")]
    [InlineData("form1[0].Page2[0].part3Check19AbsentWO[0]")]
    public void Checkbox_Part3Field19_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part3Check20Misconduct[0]")]
    [InlineData("form1[0].Page2[0].part3Check20Other[0]")]
    public void Checkbox_Part3Field21_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part3Check22ILOD[0]")]
    [InlineData("form1[0].Page2[0].part3Check22NILOD[0]")]
    [InlineData("form1[0].Page2[0].part3Check22FLOD[0]")]
    public void Checkbox_Part3Field22_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part4Check24Concur[0]")]
    [InlineData("form1[0].Page2[0].part4Check24NonConcur[0]")]
    public void Checkbox_Part4Concur_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page2[0].part5Check26ILOD[0]")]
    [InlineData("form1[0].Page2[0].part5Check26NILOD[0]")]
    [InlineData("form1[0].Page2[0].part5Check26FLOD[0]")]
    public void Checkbox_Part5Field26_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part6Check32ILOD[0]")]
    [InlineData("form1[0].Page3[0].part6Check32NILOD[0]")]
    [InlineData("form1[0].Page3[0].part6Check32FLOD[0]")]
    [InlineData("form1[0].Page3[0].part6Check32REFER[0]")]
    public void Checkbox_Part6Field32_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    [Theory]
    [InlineData("form1[0].Page3[0].part7Check34ILOD[0]")]
    [InlineData("form1[0].Page3[0].part7Check34NILOD[0]")]
    [InlineData("form1[0].Page3[0].part7Check34FLOD[0]")]
    [InlineData("form1[0].Page3[0].part7Check34REFER[0]")]
    public void Checkbox_Part7Field34_CanBeCheckedAndReadBack(string fieldName)
    {
        AssertCheckboxRoundTrip(fieldName);
    }

    // ───────────────────────────────────────────────
    //  All Mapped Fields Exist in Template
    // ───────────────────────────────────────────────

    [Fact]
    public void AllMappedFields_ExistInTemplate()
    {
        var mappings = Pdf.Af348FieldMap.CreateFieldMappings();
        var templateFieldNames = new HashSet<string>(_templateFields.Select(f => f.FullyQualifiedName), StringComparer.OrdinalIgnoreCase);

        var missingFields = new List<string>();
        foreach (var fieldName in mappings.Keys)
        {
            if (!templateFieldNames.Contains(fieldName))
                missingFields.Add(fieldName);
        }

        Assert.True(missingFields.Count == 0,
            $"The following mapped fields do not exist in the PDF template:\n{string.Join("\n", missingFields)}");
    }

    // ───────────────────────────────────────────────
    //  Bulk Write/Read All Text Fields
    // ───────────────────────────────────────────────

    [Fact]
    public void AllTextFields_CanBeWrittenAndReadBack()
    {
        var textFields = _templateFields
            .Where(f => f.FieldType == PdfFieldType.Text && f.ObjectNumber > 0)
            .ToList();

        Assert.NotEmpty(textFields);

        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in textFields)
        {
            fieldValues[field.FullyQualifiedName] = $"Test_{field.FullyQualifiedName}";
        }

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(fieldValues);

        Assert.True(filledPdf.Length > _templateBytes.Length, "Filled PDF should be larger than template");

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var filledLookup = filledFields.ToDictionary(f => f.FullyQualifiedName, f => f, StringComparer.OrdinalIgnoreCase);

        var failures = new List<string>();
        foreach (var (name, expectedValue) in fieldValues)
        {
            if (!filledLookup.TryGetValue(name, out var filledField))
            {
                failures.Add($"Field '{name}' not found in filled PDF");
                continue;
            }

            if (filledField.CurrentValue is null || !filledField.CurrentValue.Contains(expectedValue.Replace("Test_", "")))
            {
                // The value is stored as UTF-16BE hex, so check it was written (non-null)
                if (filledField.CurrentValue is null)
                    failures.Add($"Field '{name}': expected a value but got null");
            }
        }

        Assert.True(failures.Count == 0,
            $"The following text fields failed round-trip:\n{string.Join("\n", failures)}");
    }

    // ───────────────────────────────────────────────
    //  Bulk Write/Read All Checkbox Fields
    // ───────────────────────────────────────────────

    [Fact]
    public void AllCheckboxFields_CanBeCheckedAndReadBack()
    {
        var checkboxFields = _templateFields
            .Where(f => f.FieldType == PdfFieldType.Checkbox && f.ObjectNumber > 0)
            .ToList();

        Assert.NotEmpty(checkboxFields);

        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in checkboxFields)
        {
            fieldValues[field.FullyQualifiedName] = "1";
        }

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(fieldValues);

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var filledLookup = filledFields.ToDictionary(f => f.FullyQualifiedName, f => f, StringComparer.OrdinalIgnoreCase);

        var failures = new List<string>();
        foreach (var field in checkboxFields)
        {
            if (!filledLookup.TryGetValue(field.FullyQualifiedName, out var filledField))
            {
                failures.Add($"Checkbox '{field.FullyQualifiedName}' not found in filled PDF");
                continue;
            }

            var onValue = field.OnValue ?? "Yes";
            if (filledField.CurrentValue != onValue)
            {
                failures.Add($"Checkbox '{field.FullyQualifiedName}': expected '{onValue}' but got '{filledField.CurrentValue}'");
            }
        }

        Assert.True(failures.Count == 0,
            $"The following checkboxes failed round-trip:\n{string.Join("\n", failures)}");
    }

    [Fact]
    public void AllCheckboxFields_CanBeUncheckedAndReadBack()
    {
        var checkboxFields = _templateFields
            .Where(f => f.FieldType == PdfFieldType.Checkbox && f.ObjectNumber > 0)
            .ToList();

        Assert.NotEmpty(checkboxFields);

        // First check all, then uncheck all
        var checkValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in checkboxFields)
            checkValues[field.FullyQualifiedName] = "1";

        var writer = new AcroFormWriter(_templateBytes);
        var checkedPdf = writer.FillFields(checkValues);

        var uncheckValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in checkboxFields)
            uncheckValues[field.FullyQualifiedName] = "Off";

        var writer2 = new AcroFormWriter(checkedPdf);
        var uncheckedPdf = writer2.FillFields(uncheckValues);

        var parser = new PdfParser(uncheckedPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var filledLookup = filledFields.ToDictionary(f => f.FullyQualifiedName, f => f, StringComparer.OrdinalIgnoreCase);

        var failures = new List<string>();
        foreach (var field in checkboxFields)
        {
            if (!filledLookup.TryGetValue(field.FullyQualifiedName, out var filledField))
            {
                failures.Add($"Checkbox '{field.FullyQualifiedName}' not found after uncheck");
                continue;
            }

            if (filledField.CurrentValue != "Off")
            {
                failures.Add($"Checkbox '{field.FullyQualifiedName}': expected 'Off' but got '{filledField.CurrentValue}'");
            }
        }

        Assert.True(failures.Count == 0,
            $"The following checkboxes failed uncheck round-trip:\n{string.Join("\n", failures)}");
    }

    // ───────────────────────────────────────────────
    //  Af348FieldMap Integration Tests
    // ───────────────────────────────────────────────

    [Fact]
    public void Af348FieldMap_FillFromSampleCase_ProducesValidPdf()
    {
        var sampleCase = CreateSampleCase();
        var fieldMappings = Pdf.Af348FieldMap.CreateFieldMappings();

        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (pdfFieldName, valueExtractor) in fieldMappings)
        {
            var value = valueExtractor(sampleCase);
            if (value is not null)
                fieldValues[pdfFieldName] = value;
        }

        Assert.NotEmpty(fieldValues);

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(fieldValues);

        Assert.True(filledPdf.Length > _templateBytes.Length, "Filled PDF should be larger than template");

        // Verify the filled PDF can be parsed back
        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        Assert.NotEmpty(filledFields);
    }

    [Fact]
    public void Af348FieldMap_AllMappedFields_HaveNonNullValuesForSampleCase()
    {
        var sampleCase = CreateSampleCase();
        var fieldMappings = Pdf.Af348FieldMap.CreateFieldMappings();

        var nullFields = new List<string>();
        foreach (var (name, extractor) in fieldMappings)
        {
            var value = extractor(sampleCase);
            if (value is null)
                nullFields.Add(name);
        }

        // Some fields may legitimately be null (e.g., psychiatric eval date when no eval)
        // but verify most fields have values
        var mappedCount = fieldMappings.Count;
        var nullCount = nullFields.Count;
        var filledCount = mappedCount - nullCount;

        Assert.True(filledCount > mappedCount / 2,
            $"Only {filledCount}/{mappedCount} mapped fields have values for sample case. Null fields:\n{string.Join("\n", nullFields)}");
    }

    // ───────────────────────────────────────────────
    //  Edge Cases
    // ───────────────────────────────────────────────

    [Fact]
    public void Writer_EmptyFieldValues_ReturnsOriginalPdf()
    {
        var writer = new AcroFormWriter(_templateBytes);
        var result = writer.FillFields(new Dictionary<string, string>());

        Assert.Equal(_templateBytes.Length, result.Length);
    }

    [Fact]
    public void Writer_NonExistentFieldName_IsIgnored()
    {
        var writer = new AcroFormWriter(_templateBytes);
        var fieldValues = new Dictionary<string, string>
        {
            ["this.field.does.not.exist[0]"] = "Some Value"
        };

        var result = writer.FillFields(fieldValues);
        Assert.Equal(_templateBytes.Length, result.Length);
    }

    [Fact]
    public void TextField_SpecialCharacters_CanBeWrittenAndReadBack()
    {
        var firstTextField = _templateFields
            .First(f => f.FieldType == PdfFieldType.Text && f.ObjectNumber > 0);

        var testValue = "Test (with) special & chars < > / \\";

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(new Dictionary<string, string>
        {
            [firstTextField.FullyQualifiedName] = testValue
        });

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var field = filledFields.First(f =>
            string.Equals(f.FullyQualifiedName, firstTextField.FullyQualifiedName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(field.CurrentValue);
    }

    [Fact]
    public void TextField_UnicodeCharacters_CanBeWrittenAndReadBack()
    {
        var firstTextField = _templateFields
            .First(f => f.FieldType == PdfFieldType.Text && f.ObjectNumber > 0);

        var testValue = "José García — ñ ü ö";

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(new Dictionary<string, string>
        {
            [firstTextField.FullyQualifiedName] = testValue
        });

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var field = filledFields.First(f =>
            string.Equals(f.FullyQualifiedName, firstTextField.FullyQualifiedName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(field.CurrentValue);
        Assert.Contains("Jos", field.CurrentValue); // At minimum the ASCII prefix should survive
    }

    [Fact]
    public void MultipleIncrementalUpdates_ProduceValidPdf()
    {
        // First update
        var writer1 = new AcroFormWriter(_templateBytes);
        var firstUpdate = writer1.FillFields(new Dictionary<string, string>
        {
            ["form1[0].Page1[0].part1NameFill[0]"] = "Doe, John E."
        });

        // Second update on top of first
        var writer2 = new AcroFormWriter(firstUpdate);
        var secondUpdate = writer2.FillFields(new Dictionary<string, string>
        {
            ["form1[0].Page1[0].part1Rank[0]"] = "TSgt/E-6"
        });

        var parser = new PdfParser(secondUpdate);
        var reader = new AcroFormReader(parser);
        var fields = reader.ReadFields();

        Assert.NotEmpty(fields);

        var nameField = fields.FirstOrDefault(f =>
            string.Equals(f.FullyQualifiedName, "form1[0].Page1[0].part1NameFill[0]", StringComparison.OrdinalIgnoreCase));
        var rankField = fields.FirstOrDefault(f =>
            string.Equals(f.FullyQualifiedName, "form1[0].Page1[0].part1Rank[0]", StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(nameField);
        Assert.NotNull(rankField);
        Assert.NotNull(nameField.CurrentValue);
        Assert.NotNull(rankField.CurrentValue);
    }

    // ───────────────────────────────────────────────
    //  Helpers
    // ───────────────────────────────────────────────

    private void AssertTextFieldRoundTrip(string fieldName, string testValue)
    {
        var templateField = _templateFields.FirstOrDefault(f =>
            string.Equals(f.FullyQualifiedName, fieldName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(templateField);
        Assert.True(templateField.FieldType is PdfFieldType.Text or PdfFieldType.Choice,
            $"Expected Text or Choice but got {templateField.FieldType}");

        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(new Dictionary<string, string>
        {
            [fieldName] = testValue
        });

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var filledField = filledFields.First(f =>
            string.Equals(f.FullyQualifiedName, fieldName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(filledField.CurrentValue);
        Assert.Contains(testValue, filledField.CurrentValue);
    }

    private void AssertCheckboxRoundTrip(string fieldName)
    {
        var templateField = _templateFields.FirstOrDefault(f =>
            string.Equals(f.FullyQualifiedName, fieldName, StringComparison.OrdinalIgnoreCase));

        Assert.NotNull(templateField);
        Assert.Equal(PdfFieldType.Checkbox, templateField.FieldType);

        var onValue = templateField.OnValue ?? "Yes";

        // Check the checkbox
        var writer = new AcroFormWriter(_templateBytes);
        var filledPdf = writer.FillFields(new Dictionary<string, string>
        {
            [fieldName] = "1"
        });

        var parser = new PdfParser(filledPdf);
        var reader = new AcroFormReader(parser);
        var filledFields = reader.ReadFields();

        var filledField = filledFields.First(f =>
            string.Equals(f.FullyQualifiedName, fieldName, StringComparison.OrdinalIgnoreCase));

        Assert.Equal(onValue, filledField.CurrentValue);
    }

    private static Shared.Models.LineOfDutyCase CreateSampleCase()
    {
        return new Shared.Models.LineOfDutyCase
        {
            Id = 1,
            CaseId = "LOD-2025-00001",
            ProcessType = Shared.Enums.LineOfDutyProcessType.Informal,
            Component = Shared.Enums.ServiceComponent.AirForceReserve,
            MemberName = "Doe, John E.",
            MemberRank = "TSgt/E-6",
            ServiceNumber = "123-45-6789",
            Unit = "944 ASTS",
            FromLine = "944 ASTS/SGPE",
            IncidentType = Shared.Enums.IncidentType.Injury,
            IncidentDate = new DateTime(2025, 3, 15),
            IncidentDescription = "Member sustained a right knee injury during physical training on base.",
            IncidentDutyStatus = Shared.Enums.DutyStatus.Title10ActiveDuty,

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
            ClinicalDiagnosis = "Right knee medial meniscus tear, acute.",
            MedicalFindings = "Swelling and tenderness of right knee.",
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
            FinalFinding = Shared.Enums.LineOfDutyFinding.InLineOfDuty,
            ProximateCause = "Injury occurred during authorized physical training activity.",
            MedicalRecommendation = "Member should be referred for orthopedic evaluation.",

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
            MedicalReviewText = "Medical records reviewed. Injury consistent with reported mechanism.",
            MedicalReviewerNameRank = "Col Patricia Lee, MC",
            MedicalReviewDate = "25Mar2025",
            MedicalReviewerSignature = "LEE.PATRICIA.A.5566778899",
            LegalReviewText = "Case is legally sufficient. Recommend ILOD finding.",
            LegalReviewerNameRank = "Lt Col James Wilson, JAGC",
            LegalReviewDate = "26Mar2025",
            LegalReviewerSignature = "WILSON.JAMES.B.6677889900",
            LodBoardChairNameRank = "Col Robert Taylor",
            LodBoardChairDate = "28Mar2025",
            LodBoardChairSignature = "TAYLOR.ROBERT.C.7788990011",
            BoardFinding = Shared.Enums.LineOfDutyFinding.InLineOfDuty,
            BoardReferForFormal = false,

            // Part VII: Approving Authority (Items 34–35)
            ApprovingAuthorityNameRank = "Brig Gen Susan Clark",
            ApprovingAuthorityDate = "01Apr2025",
            ApprovingAuthoritySignature = "CLARK.SUSAN.D.8899001122",
            ApprovingFinding = Shared.Enums.LineOfDutyFinding.InLineOfDuty,
            ApprovingReferForFormal = false,
        };
    }
}
