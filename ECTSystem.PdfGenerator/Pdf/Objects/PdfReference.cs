namespace ECTSystem.PdfGenerator.Pdf.Objects;

public sealed class PdfReference(int objectNumber, int generation) : PdfObject
{
    public int ObjectNumber { get; } = objectNumber;
    public int Generation { get; } = generation;
    public override string ToString() => $"{ObjectNumber} {Generation} R";
}
