using System.Globalization;
using System.Text;
using FormValidationExperiments.PdfGenerator.Pdf.Objects;
using FormValidationExperiments.PdfGenerator.Pdf.Parsing;

namespace FormValidationExperiments.PdfGenerator.Pdf.AcroForm;

public sealed class AcroFormWriter
{
    private readonly byte[] _originalPdf;

    public AcroFormWriter(byte[] templatePdf)
    {
        _originalPdf = templatePdf;
    }

    /// <summary>
    /// Fills form fields and returns the complete PDF bytes (original + incremental update appended).
    /// </summary>
    public byte[] FillFields(Dictionary<string, string> fieldValues)
    {
        var parser = new PdfParser(_originalPdf);
        var reader = new AcroFormReader(parser);
        var fields = reader.ReadFields();

        // Build lookup of fields by name
        var fieldsByName = new Dictionary<string, AcroFormField>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            fieldsByName[field.FullyQualifiedName] = field;
        }

        // Determine which fields to update
        var updates = new List<(AcroFormField Field, string NewValue)>();
        foreach (var (name, value) in fieldValues)
        {
            if (fieldsByName.TryGetValue(name, out var field) && field.ObjectNumber > 0)
            {
                updates.Add((field, value));
            }
        }

        if (updates.Count == 0)
            return _originalPdf; // nothing to update

        // Start building incremental update
        using var ms = new MemoryStream();
        ms.Write(_originalPdf);

        // Ensure we start on a new line
        if (_originalPdf.Length > 0 && _originalPdf[^1] != '\n')
            WriteAscii(ms, "\n");

        // Track new object offsets for the xref table
        var newXRefEntries = new List<(int ObjectNumber, int Generation, long Offset)>();

        // Write modified field objects
        foreach (var (field, newValue) in updates)
        {
            var offset = ms.Position;
            var originalDict = parser.GetObjectDictionary(field.ObjectNumber);
            var entry = parser.XRefTable.GetEntry(field.ObjectNumber);
            var gen = entry?.Generation ?? 0;

            WriteModifiedFieldObject(ms, field, originalDict, newValue, gen, parser);
            newXRefEntries.Add((field.ObjectNumber, gen, offset));
        }

        // Write modified AcroForm dictionary with /NeedAppearances true
        if (reader.AcroFormObjectNumber > 0)
        {
            var acroFormOffset = ms.Position;
            var acroFormDict = parser.GetObjectDictionary(reader.AcroFormObjectNumber);
            var acroFormEntry = parser.XRefTable.GetEntry(reader.AcroFormObjectNumber);
            var acroFormGen = acroFormEntry?.Generation ?? 0;

            WriteModifiedAcroFormObject(ms, reader.AcroFormObjectNumber, acroFormDict, acroFormGen, parser);
            newXRefEntries.Add((reader.AcroFormObjectNumber, acroFormGen, acroFormOffset));
        }

        // Write new xref table
        var newXRefOffset = ms.Position;
        WriteXRefTable(ms, newXRefEntries, parser.XRefTable);

        // Write new trailer
        WriteTrailer(ms, parser, newXRefEntries, newXRefOffset);

        return ms.ToArray();
    }

    private void WriteModifiedFieldObject(
        MemoryStream ms,
        AcroFormField field,
        PdfDictionary originalDict,
        string newValue,
        int generation,
        PdfParser parser)
    {
        WriteAscii(ms, $"{field.ObjectNumber} {generation} obj\n");
        WriteAscii(ms, "<<\n");

        // Write all original entries except /V, /AS, and /AP for text fields only.
        // For checkboxes/radios, keep /AP so the existing appearance streams are preserved —
        // the /AS entry selects which appearance to display.
        // For text fields, clear /AP to force regeneration via /NeedAppearances.
        var isCheckboxOrRadio = field.FieldType is PdfFieldType.Checkbox or PdfFieldType.Radio;
        foreach (var (key, value) in originalDict.Entries)
        {
            if (key is "V" or "AS")
                continue;
            if (key is "AP" && !isCheckboxOrRadio)
                continue;

            WriteAscii(ms, $"/{key} ");
            WritePdfObject(ms, value);
            WriteAscii(ms, "\n");
        }

        // Write new value
        if (field.FieldType is PdfFieldType.Checkbox or PdfFieldType.Radio)
        {
            // Checkbox/Radio: value is a name (/Yes or /Off)
            var isChecked = newValue is "Yes" or "yes" or "true" or "True" or "1";
            var onValue = field.OnValue ?? "Yes";
            var nameValue = isChecked ? onValue : "Off";

            WriteAscii(ms, $"/V /{nameValue}\n");
            WriteAscii(ms, $"/AS /{nameValue}\n");
        }
        else
        {
            // Text field: value is a hex string
            var hexValue = ToHexString(newValue);
            WriteAscii(ms, $"/V {hexValue}\n");
        }

        WriteAscii(ms, ">>\n");
        WriteAscii(ms, "endobj\n");
    }

    private void WriteModifiedAcroFormObject(
        MemoryStream ms,
        int objectNumber,
        PdfDictionary originalDict,
        int generation,
        PdfParser parser)
    {
        WriteAscii(ms, $"{objectNumber} {generation} obj\n");
        WriteAscii(ms, "<<\n");

        var wroteNeedAppearances = false;

        foreach (var (key, value) in originalDict.Entries)
        {
            if (key == "NeedAppearances")
            {
                WriteAscii(ms, "/NeedAppearances true\n");
                wroteNeedAppearances = true;
                continue;
            }

            WriteAscii(ms, $"/{key} ");
            WritePdfObject(ms, value);
            WriteAscii(ms, "\n");
        }

        if (!wroteNeedAppearances)
            WriteAscii(ms, "/NeedAppearances true\n");

        WriteAscii(ms, ">>\n");
        WriteAscii(ms, "endobj\n");
    }

    private static void WriteXRefTable(
        MemoryStream ms,
        List<(int ObjectNumber, int Generation, long Offset)> entries,
        XRefTable originalXRef)
    {
        WriteAscii(ms, "xref\n");

        // Sort entries by object number
        var sorted = entries.OrderBy(e => e.ObjectNumber).ToList();

        // Write subsections (group consecutive object numbers)
        var i = 0;
        while (i < sorted.Count)
        {
            var start = sorted[i].ObjectNumber;
            var end = start;

            // Find consecutive range
            while (i + 1 < sorted.Count && sorted[i + 1].ObjectNumber == end + 1)
            {
                end++;
                i++;
            }

            var count = end - start + 1;
            WriteAscii(ms, $"{start} {count}\n");

            for (var j = 0; j < count; j++)
            {
                var entry = sorted[i - count + 1 + j];
                // Format: 10-digit offset, space, 5-digit generation, space, n, CR, LF (exactly 20 bytes)
                var line = string.Format(
                    CultureInfo.InvariantCulture,
                    "{0:D10} {1:D5} n\r\n",
                    entry.Offset,
                    entry.Generation);
                WriteAscii(ms, line);
            }

            i++;
        }
    }

    private static void WriteTrailer(
        MemoryStream ms,
        PdfParser parser,
        List<(int ObjectNumber, int Generation, long Offset)> newEntries,
        long newXRefOffset)
    {
        // Calculate /Size: max object number + 1
        var maxObj = Math.Max(
            parser.XRefTable.NextObjectNumber,
            newEntries.Count > 0 ? newEntries.Max(e => e.ObjectNumber) + 1 : 0);

        var rootRef = parser.Trailer.GetReference("Root");

        WriteAscii(ms, "trailer\n");
        WriteAscii(ms, "<<\n");
        WriteAscii(ms, $"/Size {maxObj}\n");
        WriteAscii(ms, $"/Prev {parser.StartXRefOffset}\n");

        if (rootRef is not null)
            WriteAscii(ms, $"/Root {rootRef.ObjectNumber} {rootRef.Generation} R\n");

        // Preserve /Info if present
        var infoRef = parser.Trailer.GetReference("Info");
        if (infoRef is not null)
            WriteAscii(ms, $"/Info {infoRef.ObjectNumber} {infoRef.Generation} R\n");

        // Preserve /ID if present (required by some viewers)
        var idObj = parser.Trailer.Get("ID");
        if (idObj is not null)
        {
            WriteAscii(ms, "/ID ");
            WritePdfObject(ms, idObj);
            WriteAscii(ms, "\n");
        }

        WriteAscii(ms, ">>\n");
        WriteAscii(ms, $"startxref\n{newXRefOffset}\n%%EOF\n");
    }

    private static void WritePdfObject(MemoryStream ms, PdfObject obj)
    {
        switch (obj)
        {
            case PdfName name:
                WriteAscii(ms, $"/{name.Value}");
                break;

            case PdfString str:
                if (str.IsHex)
                    WriteAscii(ms, $"<{str.Value}>");
                else
                    WriteAscii(ms, $"({EscapePdfString(str.Value)})");
                break;

            case PdfNumber num:
                WriteAscii(ms, num.ToString());
                break;

            case PdfBoolean b:
                WriteAscii(ms, b.Value ? "true" : "false");
                break;

            case PdfNull:
                WriteAscii(ms, "null");
                break;

            case PdfReference r:
                WriteAscii(ms, $"{r.ObjectNumber} {r.Generation} R");
                break;

            case PdfArray arr:
                WriteAscii(ms, "[");
                for (var i = 0; i < arr.Items.Count; i++)
                {
                    if (i > 0) WriteAscii(ms, " ");
                    WritePdfObject(ms, arr.Items[i]);
                }
                WriteAscii(ms, "]");
                break;

            case PdfDictionary dict:
                WriteAscii(ms, "<< ");
                foreach (var (key, value) in dict.Entries)
                {
                    WriteAscii(ms, $"/{key} ");
                    WritePdfObject(ms, value);
                    WriteAscii(ms, " ");
                }
                WriteAscii(ms, ">>");
                break;
        }
    }

    private static string ToHexString(string text)
    {
        // Use UTF-16BE with BOM for Unicode safety
        var bom = new byte[] { 0xFE, 0xFF };
        var encoded = Encoding.BigEndianUnicode.GetBytes(text);
        var all = new byte[bom.Length + encoded.Length];
        bom.CopyTo(all, 0);
        encoded.CopyTo(all, bom.Length);
        return "<" + Convert.ToHexString(all) + ">";
    }

    private static string EscapePdfString(string text)
    {
        return text
            .Replace("\\", "\\\\")
            .Replace("(", "\\(")
            .Replace(")", "\\)");
    }

    private static void WriteAscii(MemoryStream ms, string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text);
        ms.Write(bytes);
    }
}
