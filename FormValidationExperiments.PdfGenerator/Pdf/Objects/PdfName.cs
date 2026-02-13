namespace FormValidationExperiments.PdfGenerator.Pdf.Objects;

public sealed class PdfName(string value) : PdfObject
{
    public string Value { get; } = value;
    public override string ToString() => $"/{Value}";
}
