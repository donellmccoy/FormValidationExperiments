namespace FormValidationExperiments.PdfGenerator.Pdf.Objects;

public sealed class PdfString(string value, bool isHex = false) : PdfObject
{
    public string Value { get; } = value;
    public bool IsHex { get; } = isHex;
    public override string ToString() => IsHex ? $"<{Value}>" : $"({Value})";
}
