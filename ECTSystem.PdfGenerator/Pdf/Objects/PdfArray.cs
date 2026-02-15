namespace ECTSystem.PdfGenerator.Pdf.Objects;

public sealed class PdfArray : PdfObject
{
    public List<PdfObject> Items { get; } = [];

    public override string ToString() =>
        $"[{string.Join(" ", Items.Select(i => i.ToString()))}]";
}
