using System.Text;

namespace FormValidationExperiments.PdfGenerator.Pdf.Parsing;

public sealed class PdfTokenizer
{
    private readonly byte[] _data;
    private int _position;

    public PdfTokenizer(byte[] data)
    {
        _data = data;
        _position = 0;
    }

    public int Position
    {
        get => _position;
        set => _position = value;
    }

    public int Length => _data.Length;
    public bool AtEnd => _position >= _data.Length;

    public void SkipWhitespaceAndComments()
    {
        while (_position < _data.Length)
        {
            var b = _data[_position];

            if (IsWhitespace(b))
            {
                _position++;
                continue;
            }

            // PDF comment: % to end of line
            if (b == '%')
            {
                _position++;
                while (_position < _data.Length && _data[_position] != '\n' && _data[_position] != '\r')
                    _position++;
                continue;
            }

            break;
        }
    }

    public string ReadToken()
    {
        SkipWhitespaceAndComments();

        if (_position >= _data.Length)
            throw new PdfParseException("Unexpected end of data", _position);

        var b = _data[_position];

        // Name: /SomeName
        if (b == '/')
            return ReadName();

        // Literal string: (text)
        if (b == '(')
            return ReadLiteralString();

        // Hex string or dictionary delimiter
        if (b == '<')
        {
            if (_position + 1 < _data.Length && _data[_position + 1] == '<')
            {
                _position += 2;
                return "<<";
            }
            return ReadHexString();
        }

        if (b == '>')
        {
            if (_position + 1 < _data.Length && _data[_position + 1] == '>')
            {
                _position += 2;
                return ">>";
            }
            _position++;
            return ">";
        }

        // Array delimiters
        if (b == '[') { _position++; return "["; }
        if (b == ']') { _position++; return "]"; }

        // Regular token (keyword or number)
        return ReadRegularToken();
    }

    public string PeekToken()
    {
        var savedPos = _position;
        var token = ReadToken();
        _position = savedPos;
        return token;
    }

    public byte PeekByte()
    {
        SkipWhitespaceAndComments();
        return _position < _data.Length ? _data[_position] : (byte)0;
    }

    /// <summary>
    /// Reads raw bytes from the stream data after the "stream" keyword.
    /// </summary>
    public byte[] ReadStreamData(int length)
    {
        // Skip the line ending after "stream" keyword (CR, LF, or CRLF)
        if (_position < _data.Length && _data[_position] == '\r')
            _position++;
        if (_position < _data.Length && _data[_position] == '\n')
            _position++;

        if (_position + length > _data.Length)
            throw new PdfParseException("Stream data extends beyond file", _position);

        var result = new byte[length];
        Array.Copy(_data, _position, result, 0, length);
        _position += length;
        return result;
    }

    /// <summary>
    /// Scans backward from the end of the file looking for a marker string.
    /// Returns the byte position where the marker starts, or -1 if not found.
    /// </summary>
    public int FindLastOccurrence(string marker)
    {
        var markerBytes = Encoding.ASCII.GetBytes(marker);
        for (var i = _data.Length - markerBytes.Length; i >= 0; i--)
        {
            var found = true;
            for (var j = 0; j < markerBytes.Length; j++)
            {
                if (_data[i + j] != markerBytes[j])
                {
                    found = false;
                    break;
                }
            }
            if (found) return i;
        }
        return -1;
    }

    private string ReadName()
    {
        _position++; // skip '/'
        var sb = new StringBuilder();
        while (_position < _data.Length)
        {
            var b = _data[_position];
            if (IsWhitespace(b) || IsDelimiter(b))
                break;

            // Handle #XX hex escape in names
            if (b == '#' && _position + 2 < _data.Length)
            {
                var hex = Encoding.ASCII.GetString(_data, _position + 1, 2);
                if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out var decoded))
                {
                    sb.Append((char)decoded);
                    _position += 3;
                    continue;
                }
            }

            sb.Append((char)b);
            _position++;
        }
        return "/" + sb;
    }

    private string ReadLiteralString()
    {
        _position++; // skip '('
        var sb = new StringBuilder();
        sb.Append('(');
        var depth = 1;

        while (_position < _data.Length && depth > 0)
        {
            var b = _data[_position];

            if (b == '\\' && _position + 1 < _data.Length)
            {
                var next = _data[_position + 1];
                sb.Append('\\');
                sb.Append((char)next);
                _position += 2;

                // Octal escape: \NNN (up to 3 digits)
                if (next >= '0' && next <= '7')
                {
                    for (var i = 0; i < 2 && _position < _data.Length; i++)
                    {
                        var c = _data[_position];
                        if (c < '0' || c > '7') break;
                        sb.Append((char)c);
                        _position++;
                    }
                }
                continue;
            }

            if (b == '(') depth++;
            if (b == ')') depth--;

            if (depth > 0)
            {
                sb.Append((char)b);
                _position++;
            }
            else
            {
                _position++; // skip closing ')'
            }
        }

        sb.Append(')');
        return sb.ToString();
    }

    private string ReadHexString()
    {
        _position++; // skip '<'
        var sb = new StringBuilder();
        sb.Append('<');

        while (_position < _data.Length)
        {
            var b = _data[_position];
            if (b == '>')
            {
                _position++;
                break;
            }
            if (!IsWhitespace(b))
                sb.Append((char)b);
            _position++;
        }

        sb.Append('>');
        return sb.ToString();
    }

    private string ReadRegularToken()
    {
        var sb = new StringBuilder();
        while (_position < _data.Length)
        {
            var b = _data[_position];
            if (IsWhitespace(b) || IsDelimiter(b))
                break;
            sb.Append((char)b);
            _position++;
        }
        return sb.ToString();
    }

    private static bool IsWhitespace(byte b) =>
        b is 0 or 9 or 10 or 12 or 13 or 32; // NUL, TAB, LF, FF, CR, SPACE

    private static bool IsDelimiter(byte b) =>
        b is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>'
           or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}'
           or (byte)'/' or (byte)'%';
}

public sealed class PdfParseException(string message, long position)
    : Exception($"PDF parse error at byte {position}: {message}")
{
    public long Position { get; } = position;
}
