using ECTSystem.PdfGenerator.Pdf.Objects;
using ECTSystem.PdfGenerator.Pdf.Parsing;

namespace ECTSystem.PdfGenerator.Pdf.AcroForm;

public sealed class AcroFormReader
{
    private readonly PdfParser _parser;
    private readonly HashSet<int> _visited = [];

    public AcroFormReader(PdfParser parser)
    {
        _parser = parser;
    }

    /// <summary>
    /// Gets the AcroForm dictionary object number (needed for incremental update).
    /// </summary>
    public int AcroFormObjectNumber { get; private set; }

    public List<AcroFormField> ReadFields()
    {
        var fields = new List<AcroFormField>();

        // Trailer -> /Root -> catalog
        var rootRef = _parser.Trailer.GetReference("Root")
            ?? throw new PdfParseException("Trailer missing /Root", 0);

        var catalog = _parser.GetObjectDictionary(rootRef.ObjectNumber);

        // Catalog -> /AcroForm
        var acroFormObj = catalog.Get("AcroForm")
            ?? throw new PdfParseException("Catalog missing /AcroForm — this PDF has no form fields", 0);

        PdfDictionary acroForm;
        if (acroFormObj is PdfReference acroFormRef)
        {
            AcroFormObjectNumber = acroFormRef.ObjectNumber;
            acroForm = _parser.GetObjectDictionary(acroFormRef.ObjectNumber);
        }
        else if (acroFormObj is PdfDictionary acroFormDict)
        {
            AcroFormObjectNumber = 0; // inline, no separate object
            acroForm = acroFormDict;
        }
        else
        {
            throw new PdfParseException("AcroForm is not a dictionary", 0);
        }

        // AcroForm -> /Fields array
        var fieldsArray = acroForm.GetArray("Fields");
        if (fieldsArray is null)
        {
            var fieldsRef = acroForm.Get("Fields");
            if (fieldsRef is PdfReference fr)
                fieldsArray = _parser.Resolve(fr) as PdfArray;
        }

        if (fieldsArray is null)
            throw new PdfParseException("AcroForm missing /Fields array", 0);

        foreach (var fieldObj in fieldsArray.Items)
        {
            CollectFields(fieldObj, "", null, fields);
        }

        return fields;
    }

    private void CollectFields(PdfObject fieldObj, string parentName, PdfFieldType? inheritedType, List<AcroFormField> results)
    {
        int objectNumber;
        int generation;
        PdfDictionary fieldDict;

        if (fieldObj is PdfReference fieldRef)
        {
            objectNumber = fieldRef.ObjectNumber;
            generation = fieldRef.Generation;

            if (!_visited.Add(objectNumber))
                return; // circular reference guard

            if (_parser.Resolve(fieldRef) is not PdfDictionary resolved)
                return;
            fieldDict = resolved;
        }
        else if (fieldObj is PdfDictionary dict)
        {
            objectNumber = 0;
            generation = 0;
            fieldDict = dict;
        }
        else
        {
            return;
        }

        // Get partial field name /T
        var partialName = GetStringValue(fieldDict, "T") ?? "";
        var fullName = string.IsNullOrEmpty(parentName)
            ? partialName
            : string.IsNullOrEmpty(partialName)
                ? parentName
                : $"{parentName}.{partialName}";

        // Determine field type (can be inherited from parent)
        var fieldType = DetermineFieldType(fieldDict) ?? inheritedType;

        // Check for /Kids — if present, this is an intermediate node
        var kids = fieldDict.GetArray("Kids");
        if (kids is null)
        {
            var kidsObj = fieldDict.Get("Kids");
            if (kidsObj is PdfReference kidsRef)
                kids = _parser.Resolve(kidsRef) as PdfArray;
        }

        if (kids is not null && kids.Items.Count > 0)
        {
            // Check if kids are widget annotations (have /Subtype /Widget) or field nodes
            var firstKid = kids.Items[0];
            var firstKidDict = firstKid is PdfReference fkr
                ? _parser.Resolve(fkr) as PdfDictionary
                : firstKid as PdfDictionary;

            var isWidgetKids = firstKidDict?.GetName("Subtype")?.Value == "Widget"
                               && firstKidDict.Get("T") is null;

            if (isWidgetKids)
            {
                // Widget annotations — this is a terminal field with multiple widgets
                // (common for radio buttons, checkboxes with multiple appearances)
                var onValue = DetectOnValue(firstKidDict);
                results.Add(new AcroFormField
                {
                    FullyQualifiedName = fullName,
                    FieldType = fieldType ?? PdfFieldType.Unknown,
                    ObjectNumber = objectNumber,
                    Generation = generation,
                    CurrentValue = GetFieldValue(fieldDict),
                    OnValue = onValue
                });
            }
            else
            {
                // Child field nodes — recurse
                foreach (var kid in kids.Items)
                {
                    CollectFields(kid, fullName, fieldType, results);
                }
            }
        }
        else
        {
            // Terminal field (no kids)
            var onValue = DetectOnValue(fieldDict);
            results.Add(new AcroFormField
            {
                FullyQualifiedName = fullName,
                FieldType = fieldType ?? PdfFieldType.Unknown,
                ObjectNumber = objectNumber,
                Generation = generation,
                CurrentValue = GetFieldValue(fieldDict),
                OnValue = onValue
            });
        }
    }

    private PdfFieldType? DetermineFieldType(PdfDictionary fieldDict)
    {
        var ft = fieldDict.GetName("FT");
        if (ft is null) return null;

        return ft.Value switch
        {
            "Tx" => PdfFieldType.Text,
            "Btn" => IsRadioButton(fieldDict) ? PdfFieldType.Radio : PdfFieldType.Checkbox,
            "Ch" => PdfFieldType.Choice,
            _ => PdfFieldType.Unknown
        };
    }

    private static bool IsRadioButton(PdfDictionary fieldDict)
    {
        // Radio buttons have the /Ff flag with bit 16 set (NoToggleToOff)
        // or bit 15 set (Radio)
        var ff = fieldDict.GetNumber("Ff");
        if (ff is null) return false;
        var flags = ff.IntValue;
        return (flags & (1 << 15)) != 0; // Radio flag
    }

    private string DetectOnValue(PdfDictionary fieldDict)
    {
        if (fieldDict is null) return null;

        // Look in /AP /N for appearance state names
        var ap = fieldDict.Get("AP");
        if (ap is PdfReference apRef)
            ap = _parser.Resolve(apRef);

        if (ap is PdfDictionary apDict)
        {
            var normal = apDict.Get("N");
            if (normal is PdfReference nRef)
                normal = _parser.Resolve(nRef);

            if (normal is PdfDictionary normalDict)
            {
                foreach (var key in normalDict.Entries.Keys)
                {
                    if (key != "Off")
                        return key;
                }
            }
        }

        return "Yes"; // default
    }

    private string GetFieldValue(PdfDictionary fieldDict)
    {
        var v = fieldDict.Get("V");
        return v switch
        {
            PdfString s => DecodeStringValue(s),
            PdfName n => n.Value,
            _ => null
        };
    }

    private static string GetStringValue(PdfDictionary dict, string key)
    {
        var obj = dict.Get(key);
        return obj switch
        {
            PdfString s => DecodeStringValue(s),
            PdfName n => n.Value,
            _ => null
        };
    }

    private static string DecodeStringValue(PdfString str)
    {
        if (str.IsHex)
        {
            // Hex string — may be UTF-16BE with BOM
            var hex = str.Value;
            var bytes = Convert.FromHexString(hex);

            // Check for UTF-16BE BOM (FE FF)
            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
                return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);

            return System.Text.Encoding.BigEndianUnicode.GetString(bytes);
        }

        // Literal string — check for UTF-16BE BOM in raw bytes
        if (str.Value.Length >= 2 && str.Value[0] == '\xFE' && str.Value[1] == '\xFF')
        {
            var bytes = new byte[str.Value.Length];
            for (var i = 0; i < str.Value.Length; i++)
                bytes[i] = (byte)str.Value[i];
            return System.Text.Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2);
        }

        return str.Value;
    }
}
