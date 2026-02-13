namespace FormValidationExperiments.PdfGenerator.Pdf.Objects;

public abstract class PdfObject;

public sealed class PdfBoolean(bool value) : PdfObject
{
    public bool Value { get; } = value;
    public override string ToString() => Value ? "true" : "false";
}

public sealed class PdfNull : PdfObject
{
    public static readonly PdfNull Instance = new();
    public override string ToString() => "null";
}
