using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Pdf.Advanced;

// Read field names from the PDF's AcroForm using low-level dictionary API
// to avoid triggering font resolution in PdfTextField constructor.
var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..",
    "ECTSystem.Api", "Templates", "AF348_06012015_Template.pdf");
path = Path.GetFullPath(path);

Console.WriteLine($"Opening: {path}");
using var doc = PdfReader.Open(path, PdfDocumentOpenMode.Import);

// Access the AcroForm dictionary directly
var acroForm = doc.Internals.Catalog.Elements.GetDictionary("/AcroForm");
if (acroForm == null)
{
    Console.WriteLine("No AcroForm dictionary found.");
    // Check for XFA
    Console.WriteLine("Checking root catalog keys:");
    foreach (var key in doc.Internals.Catalog.Elements.Keys)
        Console.WriteLine($"  {key}");
    return;
}

Console.WriteLine("AcroForm keys:");
foreach (var key in acroForm.Elements.Keys)
    Console.WriteLine($"  {key}");

// Check for XFA form
var xfa = acroForm.Elements["/XFA"];
if (xfa != null)
{
    Console.WriteLine();
    Console.WriteLine("WARNING: This PDF contains XFA form data (Adobe LiveCycle Designer).");
    Console.WriteLine("XFA forms are NOT standard AcroForms. PDFsharp has limited XFA support.");
    Console.WriteLine($"XFA type: {xfa.GetType().Name}");
}

// Get the /Fields array
var fields = acroForm.Elements.GetArray("/Fields");
if (fields == null)
{
    Console.WriteLine("No /Fields array in AcroForm.");
    return;
}

Console.WriteLine($"\nTop-level fields: {fields.Elements.Count}");
Console.WriteLine();
Console.WriteLine("Name | Type | FieldType | Value");
Console.WriteLine(new string('-', 100));

void PrintFieldDict(PdfArray fieldArray, string indent = "")
{
    for (int i = 0; i < fieldArray.Elements.Count; i++)
    {
        var item = fieldArray.Elements[i];
        PdfDictionary dict;
        if (item is PdfReference reference)
            dict = reference.Value as PdfDictionary;
        else
            dict = item as PdfDictionary;
        if (dict == null) continue;

        var name = dict.Elements.GetString("/T") ?? "(no /T)";
        var fullName = dict.Elements.GetString("/TU") ?? "";
        var fieldType = dict.Elements.GetString("/FT") ?? "(inherited)";
        var value = dict.Elements.GetString("/V") ?? "(empty)";
        var tooltip = fullName != "" ? $" [{fullName}]" : "";
        Console.WriteLine($"{indent}{name} | {fieldType} | {value}{tooltip}");

        // Recurse into /Kids
        var kids = dict.Elements.GetArray("/Kids");
        if (kids != null)
        {
            PrintFieldDict(kids, indent + "  ");
        }
    }
}

PrintFieldDict(fields);
