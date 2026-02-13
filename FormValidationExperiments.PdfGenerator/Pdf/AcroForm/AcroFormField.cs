namespace FormValidationExperiments.PdfGenerator.Pdf.AcroForm;

public enum PdfFieldType
{
    Text,
    Checkbox,
    Radio,
    Choice,
    Unknown
}

public sealed class AcroFormField
{
    public required string FullyQualifiedName { get; init; }
    public required PdfFieldType FieldType { get; init; }
    public required int ObjectNumber { get; init; }
    public required int Generation { get; init; }
    public string? CurrentValue { get; init; }
    public string? OnValue { get; init; }
}
