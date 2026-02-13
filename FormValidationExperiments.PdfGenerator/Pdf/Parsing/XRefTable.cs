namespace FormValidationExperiments.PdfGenerator.Pdf.Parsing;

public sealed class XRefEntry
{
    public required int ObjectNumber { get; init; }
    public required int Generation { get; init; }
    public required long ByteOffset { get; init; }
    public required bool InUse { get; init; }

    /// <summary>
    /// For type-2 xref entries (objects in object streams): the object number of the containing ObjStm.
    /// -1 for normal (type-1) objects.
    /// </summary>
    public int ObjStreamNumber { get; init; } = -1;

    /// <summary>
    /// For type-2 xref entries: the index within the object stream.
    /// </summary>
    public int ObjStreamIndex { get; init; }

    public bool IsInObjectStream => ObjStreamNumber >= 0;
}

public sealed class XRefTable
{
    private readonly Dictionary<int, XRefEntry> _entries = [];

    public void AddEntry(XRefEntry entry)
    {
        _entries[entry.ObjectNumber] = entry;
    }

    public XRefEntry? GetEntry(int objectNumber) =>
        _entries.GetValueOrDefault(objectNumber);

    public int NextObjectNumber =>
        _entries.Count == 0 ? 1 : _entries.Keys.Max() + 1;

    public IReadOnlyDictionary<int, XRefEntry> Entries => _entries;
}
