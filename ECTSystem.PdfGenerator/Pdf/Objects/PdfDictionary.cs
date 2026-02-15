namespace ECTSystem.PdfGenerator.Pdf.Objects;

public sealed class PdfDictionary : PdfObject
{
    public Dictionary<string, PdfObject> Entries { get; } = new(StringComparer.Ordinal);

    public PdfName GetName(string key) =>
        Entries.GetValueOrDefault(key) as PdfName;

    public PdfString GetString(string key) =>
        Entries.GetValueOrDefault(key) as PdfString;

    public PdfNumber GetNumber(string key) =>
        Entries.GetValueOrDefault(key) as PdfNumber;

    public PdfArray GetArray(string key) =>
        Entries.GetValueOrDefault(key) as PdfArray;

    public PdfDictionary GetDictionary(string key) =>
        Entries.GetValueOrDefault(key) as PdfDictionary;

    public PdfReference GetReference(string key) =>
        Entries.GetValueOrDefault(key) as PdfReference;

    public PdfObject Get(string key) =>
        Entries.GetValueOrDefault(key);

    public bool ContainsKey(string key) =>
        Entries.ContainsKey(key);
}
