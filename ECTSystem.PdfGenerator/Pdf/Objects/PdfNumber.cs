using System.Globalization;

namespace ECTSystem.PdfGenerator.Pdf.Objects;

public sealed class PdfNumber(double value) : PdfObject
{
    public double Value { get; } = value;
    public int IntValue => (int)Value;

    public override string ToString() =>
        Value == Math.Floor(Value)
            ? IntValue.ToString(CultureInfo.InvariantCulture)
            : Value.ToString("G", CultureInfo.InvariantCulture);
}
