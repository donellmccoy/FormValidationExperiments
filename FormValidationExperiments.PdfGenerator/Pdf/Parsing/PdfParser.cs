using System.Globalization;
using System.IO.Compression;
using FormValidationExperiments.PdfGenerator.Pdf.Objects;

namespace FormValidationExperiments.PdfGenerator.Pdf.Parsing;

public sealed class PdfParser
{
    private readonly byte[] _data;
    private readonly PdfTokenizer _tokenizer;
    private readonly Dictionary<int, PdfObject> _objectCache = [];

    public XRefTable XRefTable { get; } = new();
    public PdfDictionary Trailer { get; private set; } = new();
    public long StartXRefOffset { get; private set; }

    public PdfParser(byte[] pdfData)
    {
        _data = pdfData;
        _tokenizer = new PdfTokenizer(pdfData);
        ParseStructure();
    }

    public PdfObject ResolveObject(int objectNumber)
    {
        if (_objectCache.TryGetValue(objectNumber, out var cached))
            return cached;

        var entry = XRefTable.GetEntry(objectNumber)
            ?? throw new PdfParseException($"Object {objectNumber} not found in xref table", 0);

        if (!entry.InUse)
            return PdfNull.Instance;

        // Object is inside an object stream — decompress and parse the ObjStm
        if (entry.IsInObjectStream)
        {
            var resolved = ResolveFromObjectStream(entry);
            _objectCache[objectNumber] = resolved;
            return resolved;
        }

        _tokenizer.Position = (int)entry.ByteOffset;

        // Parse: N G obj <value> endobj
        var objNumToken = _tokenizer.ReadToken();
        var genToken = _tokenizer.ReadToken();
        var objKeyword = _tokenizer.ReadToken();

        if (objKeyword != "obj")
            throw new PdfParseException($"Expected 'obj' keyword, got '{objKeyword}'", _tokenizer.Position);

        var value = ParseValue();

        // Check for stream
        _tokenizer.SkipWhitespaceAndComments();
        if (!_tokenizer.AtEnd && _tokenizer.Position + 6 <= _data.Length)
        {
            var peek = System.Text.Encoding.ASCII.GetString(
                _data, _tokenizer.Position, Math.Min(6, _data.Length - _tokenizer.Position));
            if (peek.StartsWith("stream") && value is PdfDictionary streamDict)
            {
                _tokenizer.Position += 6; // skip "stream"
                var length = GetStreamLength(streamDict);
                _tokenizer.ReadStreamData(length);
                // Stream data skipped — not needed for AcroForm dict objects
            }
        }

        _objectCache[objectNumber] = value;
        return value;
    }

    public PdfObject Resolve(PdfObject obj) =>
        obj is PdfReference r ? ResolveObject(r.ObjectNumber) : obj;

    public PdfDictionary GetObjectDictionary(int objectNumber)
    {
        var obj = ResolveObject(objectNumber);
        return obj as PdfDictionary
            ?? throw new PdfParseException($"Object {objectNumber} is not a dictionary", 0);
    }

    // ─── Object Stream Support ───

    // Cache of parsed object streams: ObjStm object number -> list of (objNum, parsed object)
    private readonly Dictionary<int, Dictionary<int, PdfObject>> _objStreamCache = [];

    private PdfObject ResolveFromObjectStream(XRefEntry entry)
    {
        if (!_objStreamCache.TryGetValue(entry.ObjStreamNumber, out var streamObjects))
        {
            streamObjects = ParseObjectStream(entry.ObjStreamNumber);
            _objStreamCache[entry.ObjStreamNumber] = streamObjects;
        }

        return streamObjects.GetValueOrDefault(entry.ObjectNumber) ?? PdfNull.Instance;
    }

    private Dictionary<int, PdfObject> ParseObjectStream(int objStreamNumber)
    {
        // The ObjStm object itself must be a normal (type 1) object
        var streamEntry = XRefTable.GetEntry(objStreamNumber)
            ?? throw new PdfParseException($"ObjStm {objStreamNumber} not found in xref table", 0);

        if (streamEntry.IsInObjectStream)
            throw new PdfParseException($"ObjStm {objStreamNumber} cannot itself be in an object stream", 0);

        // Parse the ObjStm indirect object to get its dictionary
        _tokenizer.Position = (int)streamEntry.ByteOffset;
        _tokenizer.ReadToken(); // obj number
        _tokenizer.ReadToken(); // generation
        var objKeyword = _tokenizer.ReadToken();
        if (objKeyword != "obj")
            throw new PdfParseException($"Expected 'obj' for ObjStm, got '{objKeyword}'", _tokenizer.Position);

        var streamDict = ParseValue() as PdfDictionary
            ?? throw new PdfParseException("ObjStm dictionary expected", _tokenizer.Position);

        // Read stream data
        _tokenizer.SkipWhitespaceAndComments();
        var peek = System.Text.Encoding.ASCII.GetString(
            _data, _tokenizer.Position, Math.Min(6, _data.Length - _tokenizer.Position));
        if (!peek.StartsWith("stream"))
            throw new PdfParseException("Expected 'stream' in ObjStm", _tokenizer.Position);
        _tokenizer.Position += 6;

        var length = GetStreamLength(streamDict);
        var rawData = _tokenizer.ReadStreamData(length);
        var decompressed = DecompressStream(rawData, streamDict);

        // Parse the object stream contents
        var n = streamDict.GetNumber("N")?.IntValue ?? 0;       // number of objects
        var first = streamDict.GetNumber("First")?.IntValue ?? 0; // byte offset to first object data

        // The stream starts with N pairs of (objNumber, byteOffset) in ASCII
        var streamTokenizer = new PdfTokenizer(decompressed);
        var objNumbers = new int[n];
        var objOffsets = new int[n];

        for (var i = 0; i < n; i++)
        {
            objNumbers[i] = int.Parse(streamTokenizer.ReadToken(), CultureInfo.InvariantCulture);
            objOffsets[i] = int.Parse(streamTokenizer.ReadToken(), CultureInfo.InvariantCulture);
        }

        // Parse each object
        var result = new Dictionary<int, PdfObject>();
        for (var i = 0; i < n; i++)
        {
            streamTokenizer.Position = first + objOffsets[i];
            var parsedObj = ParseValueFromTokenizer(streamTokenizer);
            result[objNumbers[i]] = parsedObj;

            // Only cache if the current xref entry still points to this object stream.
            // Incremental updates may override objects with type-1 entries at new byte offsets;
            // caching them here would shadow the newer version.
            var xrefEntry = XRefTable.GetEntry(objNumbers[i]);
            if (xrefEntry is not null && xrefEntry.IsInObjectStream && xrefEntry.ObjStreamNumber == objStreamNumber)
            {
                _objectCache[objNumbers[i]] = parsedObj;
            }
        }

        return result;
    }

    /// <summary>
    /// Parses a value using the given tokenizer (for object stream parsing).
    /// </summary>
    private PdfObject ParseValueFromTokenizer(PdfTokenizer tokenizer)
    {
        // Save and swap tokenizer
        var savedPos = _tokenizer.Position;
        var origTokenizer = _tokenizer;

        // We need to use a separate approach since _tokenizer is readonly
        // Instead, temporarily set position on the data and parse
        // Actually, we'll create a mini-parser for the decompressed data

        tokenizer.SkipWhitespaceAndComments();
        if (tokenizer.AtEnd)
            return PdfNull.Instance;

        var token = tokenizer.PeekToken();

        if (token == "<<")
            return ParseDictionaryFrom(tokenizer);

        if (token == "[")
            return ParseArrayFrom(tokenizer);

        tokenizer.ReadToken();

        if (token.StartsWith('/'))
            return new PdfName(token[1..]);
        if (token.StartsWith('('))
            return ParseLiteralStringValue(token);
        if (token.StartsWith('<'))
            return new PdfString(token[1..^1], isHex: true);
        if (token == "true") return new PdfBoolean(true);
        if (token == "false") return new PdfBoolean(false);
        if (token == "null") return PdfNull.Instance;

        if (IsNumeric(token))
        {
            var sp = tokenizer.Position;
            tokenizer.SkipWhitespaceAndComments();
            if (!tokenizer.AtEnd)
            {
                var second = tokenizer.PeekToken();
                if (IsNumeric(second))
                {
                    tokenizer.ReadToken();
                    tokenizer.SkipWhitespaceAndComments();
                    if (!tokenizer.AtEnd && tokenizer.PeekToken() == "R")
                    {
                        tokenizer.ReadToken();
                        return new PdfReference(
                            int.Parse(token, CultureInfo.InvariantCulture),
                            int.Parse(second, CultureInfo.InvariantCulture));
                    }
                    tokenizer.Position = sp;
                }
            }
            return new PdfNumber(double.Parse(token, CultureInfo.InvariantCulture));
        }

        return new PdfName(token);
    }

    private PdfDictionary ParseDictionaryFrom(PdfTokenizer tokenizer)
    {
        tokenizer.ReadToken(); // consume "<<"
        var dict = new PdfDictionary();
        while (true)
        {
            tokenizer.SkipWhitespaceAndComments();
            if (tokenizer.PeekToken() == ">>") { tokenizer.ReadToken(); break; }
            var keyToken = tokenizer.ReadToken();
            if (!keyToken.StartsWith('/'))
                throw new PdfParseException($"Expected name in dict, got '{keyToken}'", tokenizer.Position);
            dict.Entries[keyToken[1..]] = ParseValueFromTokenizer(tokenizer);
        }
        return dict;
    }

    private PdfArray ParseArrayFrom(PdfTokenizer tokenizer)
    {
        tokenizer.ReadToken(); // consume "["
        var array = new PdfArray();
        while (true)
        {
            tokenizer.SkipWhitespaceAndComments();
            if (tokenizer.PeekToken() == "]") { tokenizer.ReadToken(); break; }
            array.Items.Add(ParseValueFromTokenizer(tokenizer));
        }
        return array;
    }

    // ─── Structure Parsing ───

    private void ParseStructure()
    {
        var startXRefPos = _tokenizer.FindLastOccurrence("startxref");
        if (startXRefPos < 0)
            throw new PdfParseException("Could not find 'startxref' marker", 0);

        _tokenizer.Position = startXRefPos + "startxref".Length;
        _tokenizer.SkipWhitespaceAndComments();
        var xrefOffsetToken = _tokenizer.ReadToken();
        var xrefOffset = long.Parse(xrefOffsetToken, CultureInfo.InvariantCulture);
        StartXRefOffset = xrefOffset;

        ParseXRefAt(xrefOffset);
    }

    private void ParseXRefAt(long offset)
    {
        _tokenizer.Position = (int)offset;
        _tokenizer.SkipWhitespaceAndComments();

        var firstToken = _tokenizer.PeekToken();

        if (firstToken == "xref")
        {
            _tokenizer.ReadToken();
            ParseTraditionalXRef();
        }
        else if (IsNumeric(firstToken))
        {
            ParseXRefStream(offset);
        }
        else
        {
            throw new PdfParseException($"Unexpected token at xref offset: '{firstToken}'", offset);
        }
    }

    // ─── Cross-Reference Stream (PDF 1.5+) ───

    private void ParseXRefStream(long offset)
    {
        _tokenizer.Position = (int)offset;

        // Parse the indirect object header: N G obj
        var objNumToken = _tokenizer.ReadToken();
        var genToken = _tokenizer.ReadToken();
        var objKeyword = _tokenizer.ReadToken();

        if (objKeyword != "obj")
            throw new PdfParseException($"Expected 'obj' for xref stream, got '{objKeyword}'", _tokenizer.Position);

        var objNum = int.Parse(objNumToken, CultureInfo.InvariantCulture);

        // Parse the stream dictionary
        var dict = ParseDictionary();

        // Verify it's an XRef stream
        var type = dict.GetName("Type");
        if (type?.Value != "XRef")
            throw new PdfParseException("Expected /Type /XRef in xref stream", _tokenizer.Position);

        // Use this as trailer if we haven't set one yet
        if (Trailer.Entries.Count == 0)
            Trailer = dict;

        // Read stream data
        _tokenizer.SkipWhitespaceAndComments();
        var streamCheck = System.Text.Encoding.ASCII.GetString(
            _data, _tokenizer.Position, Math.Min(6, _data.Length - _tokenizer.Position));
        if (!streamCheck.StartsWith("stream"))
            throw new PdfParseException("Expected 'stream' keyword", _tokenizer.Position);
        _tokenizer.Position += 6;

        var length = GetStreamLength(dict);
        var rawStreamData = _tokenizer.ReadStreamData(length);

        // Decompress if needed
        var decompressed = DecompressStream(rawStreamData, dict);

        // Parse xref entries from the decompressed data
        ParseXRefStreamEntries(dict, decompressed);

        // Follow /Prev chain
        var prev = dict.GetNumber("Prev");
        if (prev is not null)
            ParseXRefAt((long)prev.Value);
    }

    private void ParseXRefStreamEntries(PdfDictionary dict, byte[] data)
    {
        // /W array specifies byte widths for each field
        var wArray = dict.GetArray("W")
            ?? throw new PdfParseException("XRef stream missing /W array", 0);
        var w = wArray.Items.Select(i => ((PdfNumber)i).IntValue).ToArray();
        if (w.Length < 3)
            throw new PdfParseException("/W array must have 3 entries", 0);

        var w0 = w[0]; // type field width
        var w1 = w[1]; // field 2 width (offset or obj stream number)
        var w2 = w[2]; // field 3 width (generation or index)
        var entrySize = w0 + w1 + w2;

        // /Size gives total number of objects
        var size = dict.GetNumber("Size")?.IntValue ?? 0;

        // /Index array specifies subsections: [start1 count1 start2 count2 ...]
        var indexArray = dict.GetArray("Index");
        List<(int Start, int Count)> subsections;

        if (indexArray is not null)
        {
            subsections = [];
            for (var i = 0; i + 1 < indexArray.Items.Count; i += 2)
            {
                var start = ((PdfNumber)indexArray.Items[i]).IntValue;
                var count = ((PdfNumber)indexArray.Items[i + 1]).IntValue;
                subsections.Add((start, count));
            }
        }
        else
        {
            // Default: single subsection [0, Size]
            subsections = [(0, size)];
        }

        var dataPos = 0;
        foreach (var (startObj, count) in subsections)
        {
            for (var i = 0; i < count; i++)
            {
                if (dataPos + entrySize > data.Length)
                    break;

                var field1 = ReadXRefField(data, dataPos, w0, defaultValue: 1); // type (default 1)
                dataPos += w0;
                var field2 = ReadXRefField(data, dataPos, w1, defaultValue: 0);
                dataPos += w1;
                var field3 = ReadXRefField(data, dataPos, w2, defaultValue: 0);
                dataPos += w2;

                var objNumber = startObj + i;

                // Only add if not already present (most recent xref takes precedence)
                if (XRefTable.GetEntry(objNumber) is not null)
                    continue;

                switch (field1)
                {
                    case 0: // free object
                        XRefTable.AddEntry(new XRefEntry
                        {
                            ObjectNumber = objNumber,
                            Generation = (int)field3,
                            ByteOffset = 0,
                            InUse = false
                        });
                        break;

                    case 1: // normal object — field2 is byte offset, field3 is generation
                        XRefTable.AddEntry(new XRefEntry
                        {
                            ObjectNumber = objNumber,
                            Generation = (int)field3,
                            ByteOffset = field2,
                            InUse = true
                        });
                        break;

                    case 2: // compressed object in object stream — field2 is stream obj number, field3 is index
                        XRefTable.AddEntry(new XRefEntry
                        {
                            ObjectNumber = objNumber,
                            Generation = 0,
                            ByteOffset = -1,
                            InUse = true,
                            ObjStreamNumber = (int)field2,
                            ObjStreamIndex = (int)field3
                        });
                        break;
                }
            }
        }
    }

    private static long ReadXRefField(byte[] data, int offset, int width, long defaultValue)
    {
        if (width == 0)
            return defaultValue;

        long value = 0;
        for (var i = 0; i < width; i++)
            value = (value << 8) | data[offset + i];
        return value;
    }

    // ─── Stream Decompression ───

    private static byte[] DecompressStream(byte[] rawData, PdfDictionary streamDict)
    {
        var filter = streamDict.GetName("Filter");
        if (filter is null)
            return rawData; // no compression

        if (filter.Value != "FlateDecode")
            throw new NotSupportedException($"Stream filter /{filter.Value} is not supported");

        // FlateDecode = zlib (2-byte header) + deflate
        // Skip the 2-byte zlib header
        using var input = new MemoryStream(rawData);

        // Skip zlib header (first 2 bytes)
        if (rawData.Length >= 2)
        {
            input.ReadByte();
            input.ReadByte();
        }

        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        var decompressed = output.ToArray();

        // Apply predictor if specified
        var decodeParms = streamDict.GetDictionary("DecodeParms");
        if (decodeParms is not null)
        {
            var predictor = decodeParms.GetNumber("Predictor")?.IntValue ?? 1;
            if (predictor >= 10) // PNG predictor
            {
                var columns = decodeParms.GetNumber("Columns")?.IntValue ?? 1;
                decompressed = ApplyPngPredictor(decompressed, columns);
            }
        }

        return decompressed;
    }

    private static byte[] ApplyPngPredictor(byte[] data, int columns)
    {
        var rowSize = columns + 1; // +1 for the predictor byte at start of each row
        var rows = data.Length / rowSize;
        var result = new byte[rows * columns];
        var prevRow = new byte[columns];

        for (var row = 0; row < rows; row++)
        {
            var filterByte = data[row * rowSize];
            var rowStart = row * rowSize + 1;

            for (var col = 0; col < columns; col++)
            {
                var raw = data[rowStart + col];
                byte decoded;

                switch (filterByte)
                {
                    case 0: // None
                        decoded = raw;
                        break;
                    case 1: // Sub
                        var left = col > 0 ? result[row * columns + col - 1] : (byte)0;
                        decoded = (byte)(raw + left);
                        break;
                    case 2: // Up
                        decoded = (byte)(raw + prevRow[col]);
                        break;
                    default:
                        decoded = raw; // Fallback
                        break;
                }

                result[row * columns + col] = decoded;
            }

            Array.Copy(result, row * columns, prevRow, 0, columns);
        }

        return result;
    }

    // ─── Traditional XRef ───

    private void ParseTraditionalXRef()
    {
        while (true)
        {
            _tokenizer.SkipWhitespaceAndComments();
            var token = _tokenizer.PeekToken();

            if (token == "trailer")
                break;

            var startObj = int.Parse(_tokenizer.ReadToken(), CultureInfo.InvariantCulture);
            var count = int.Parse(_tokenizer.ReadToken(), CultureInfo.InvariantCulture);

            for (var i = 0; i < count; i++)
            {
                var offsetStr = _tokenizer.ReadToken();
                var genStr = _tokenizer.ReadToken();
                var marker = _tokenizer.ReadToken();

                var entryOffset = long.Parse(offsetStr, CultureInfo.InvariantCulture);
                var generation = int.Parse(genStr, CultureInfo.InvariantCulture);
                var inUse = marker == "n";
                var objNum = startObj + i;

                if (XRefTable.GetEntry(objNum) is null)
                {
                    XRefTable.AddEntry(new XRefEntry
                    {
                        ObjectNumber = objNum,
                        Generation = generation,
                        ByteOffset = entryOffset,
                        InUse = inUse
                    });
                }
            }
        }

        _tokenizer.ReadToken(); // consume "trailer"
        var trailerDict = ParseDictionary();

        if (Trailer.Entries.Count == 0)
            Trailer = trailerDict;

        var prev = trailerDict.GetNumber("Prev");
        if (prev is not null)
            ParseXRefAt((long)prev.Value);
    }

    // ─── Value Parsing ───

    private PdfObject ParseValue()
    {
        _tokenizer.SkipWhitespaceAndComments();

        if (_tokenizer.AtEnd)
            throw new PdfParseException("Unexpected end of data", _tokenizer.Position);

        var token = _tokenizer.PeekToken();

        if (token == "<<")
            return ParseDictionary();

        if (token == "[")
            return ParseArray();

        _tokenizer.ReadToken();

        if (token.StartsWith('/'))
            return new PdfName(token[1..]);

        if (token.StartsWith('('))
            return ParseLiteralStringValue(token);

        if (token.StartsWith('<'))
            return new PdfString(token[1..^1], isHex: true);

        if (token == "true") return new PdfBoolean(true);
        if (token == "false") return new PdfBoolean(false);
        if (token == "null") return PdfNull.Instance;

        if (IsNumeric(token))
        {
            var savedPos = _tokenizer.Position;
            _tokenizer.SkipWhitespaceAndComments();

            if (!_tokenizer.AtEnd)
            {
                var secondToken = _tokenizer.PeekToken();
                if (IsNumeric(secondToken))
                {
                    _tokenizer.ReadToken();
                    _tokenizer.SkipWhitespaceAndComments();

                    if (!_tokenizer.AtEnd && _tokenizer.PeekToken() == "R")
                    {
                        _tokenizer.ReadToken();
                        return new PdfReference(
                            int.Parse(token, CultureInfo.InvariantCulture),
                            int.Parse(secondToken, CultureInfo.InvariantCulture));
                    }

                    _tokenizer.Position = savedPos;
                }
            }

            return new PdfNumber(double.Parse(token, CultureInfo.InvariantCulture));
        }

        return new PdfName(token);
    }

    private PdfDictionary ParseDictionary()
    {
        _tokenizer.ReadToken(); // consume "<<"
        var dict = new PdfDictionary();

        while (true)
        {
            _tokenizer.SkipWhitespaceAndComments();
            var token = _tokenizer.PeekToken();

            if (token == ">>")
            {
                _tokenizer.ReadToken();
                break;
            }

            var keyToken = _tokenizer.ReadToken();
            if (!keyToken.StartsWith('/'))
                throw new PdfParseException($"Expected name key in dictionary, got '{keyToken}'", _tokenizer.Position);

            var key = keyToken[1..];
            var value = ParseValue();
            dict.Entries[key] = value;
        }

        return dict;
    }

    private PdfArray ParseArray()
    {
        _tokenizer.ReadToken(); // consume "["
        var array = new PdfArray();

        while (true)
        {
            _tokenizer.SkipWhitespaceAndComments();
            var token = _tokenizer.PeekToken();

            if (token == "]")
            {
                _tokenizer.ReadToken();
                break;
            }

            array.Items.Add(ParseValue());
        }

        return array;
    }

    private static PdfString ParseLiteralStringValue(string raw)
    {
        var inner = raw[1..^1];
        var sb = new System.Text.StringBuilder();

        for (var i = 0; i < inner.Length; i++)
        {
            if (inner[i] == '\\' && i + 1 < inner.Length)
            {
                i++;
                switch (inner[i])
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case '(': sb.Append('('); break;
                    case ')': sb.Append(')'); break;
                    case '\\': sb.Append('\\'); break;
                    default:
                        if (inner[i] >= '0' && inner[i] <= '7')
                        {
                            var octal = new string(inner[i], 1);
                            for (var j = 1; j < 3 && i + j < inner.Length && inner[i + j] >= '0' && inner[i + j] <= '7'; j++)
                                octal += inner[i + j];
                            sb.Append((char)Convert.ToInt32(octal, 8));
                            i += octal.Length - 1;
                        }
                        else
                        {
                            sb.Append(inner[i]);
                        }
                        break;
                }
            }
            else
            {
                sb.Append(inner[i]);
            }
        }

        return new PdfString(sb.ToString());
    }

    private int GetStreamLength(PdfDictionary streamDict)
    {
        var lengthObj = streamDict.Get("Length");
        return lengthObj switch
        {
            PdfNumber num => num.IntValue,
            PdfReference r => ((PdfNumber)ResolveObject(r.ObjectNumber)).IntValue,
            _ => throw new PdfParseException("Stream missing /Length", _tokenizer.Position)
        };
    }

    private static bool IsNumeric(string token)
    {
        if (string.IsNullOrEmpty(token)) return false;
        var start = 0;
        if (token[0] == '-' || token[0] == '+') start = 1;
        if (start >= token.Length) return false;
        var hasDot = false;
        for (var i = start; i < token.Length; i++)
        {
            if (token[i] == '.')
            {
                if (hasDot) return false;
                hasDot = true;
            }
            else if (token[i] < '0' || token[i] > '9')
            {
                return false;
            }
        }
        return true;
    }
}
