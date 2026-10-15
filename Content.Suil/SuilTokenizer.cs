using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Diagnostics.Contracts;

namespace Content.Suil;

/// <summary>
/// SUIL (Styled UI Layout) text tokenizer. It is built on <see cref="SuilCodePointStream"/> in the same way as
/// <see cref="Content.Client._RF.CSS.CssTokenizer"/> and keeps its zero tolerance for syntax errors:
/// a <see cref="SuilTokenizeError"/> is thrown for anything the language can't contain.
/// Differences from CSS:
/// <list type="bullet">
/// <item>comments are <c>//</c> and <c>/* */</c> and are turned into a whitespace token;</item>
/// <item>strings are double-quoted and may contain <c>`variable`</c> references;</item>
/// <item><c>`name`</c>, <c>$loc-key</c>, <c>%content%</c> and the single <c>_</c> are tokens of their own;</item>
/// <item>there are no urls, at-keywords, escapes in idents, custom properties and CDO/CDC.</item>
/// </list>
/// </summary>
public static class SuilTokenizer
{
    private static bool IsEof(int cp) => cp == -1;

    private static bool IsDigit(int cp) => cp is >= 0x0030 and <= 0x0039;

    private static bool IsLetter(int cp) => cp is >= 0x0041 and <= 0x005A or >= 0x0061 and <= 0x007A;

    private static bool IsNonAscii(int cp) => cp >= 0x0080;

    /// <summary>
    /// A letter or a non-ASCII code point. The underscore can't start an ident: the single <c>_</c> is a token.
    /// </summary>
    private static bool IsIdentStart(int cp) => IsLetter(cp) || IsNonAscii(cp);

    /// <summary>
    /// An ident-start code point, a digit, U+002D HYPHEN-MINUS (-) or U+005F LOW LINE (_).
    /// </summary>
    private static bool IsIdentChar(int cp) => IsIdentStart(cp) || IsDigit(cp) || cp is 0x002D or 0x005F;

    /// <summary>
    /// An ident code point or U+002E FULL STOP (.), which separates the attribute of a Fluent message.
    /// </summary>
    private static bool IsLocChar(int cp) => IsIdentChar(cp) || cp == 0x002E;

    private static bool IsNewline(int cp) => cp == 0x000A;

    private static bool IsWhitespace(int cp) => IsNewline(cp) || cp is 0x0009 or 0x0020;

    private readonly record struct ConsumedNumber(string Representation, bool IsInteger, double Value);

    private readonly struct LineMap
    {
        private readonly int[] _lineStarts;

        public LineMap(int[] codePoints)
        {
            var starts = new List<int> { 0 };

            for (var i = 0; i < codePoints.Length; i++)
            {
                if (IsNewline(codePoints[i]))
                    starts.Add(i + 1);
            }

            _lineStarts = starts.ToArray();
        }

        public (int Line, int Column) Locate(int index)
        {
            index = Math.Max(index, 0);

            // the last line that starts at or before the index
            var low = 0;
            var high = _lineStarts.Length - 1;

            while (low < high)
            {
                var middle = (low + high + 1) / 2;

                if (_lineStarts[middle] <= index)
                    low = middle;
                else
                    high = middle - 1;
            }

            return (low + 1, index - _lineStarts[low] + 1);
        }
    }

    /// <summary>
    /// Splits text into Unicode code points. A lone surrogate becomes U+FFFD, as the CSS preprocessing does.
    /// </summary>
    private static int[] ToCodePoints(string text)
    {
        var result = new List<int>(text.Length);

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];

            if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                result.Add(char.ConvertToUtf32(c, text[i + 1]));
                i++;
            }
            else if (char.IsSurrogate(c))
            {
                result.Add(0xFFFD);
            }
            else
            {
                result.Add(c);
            }
        }

        return result.ToArray();
    }

    private static string CodePointToString(int codePoint) => char.ConvertFromUtf32(codePoint);

    private static string Preprocess(string input)
    {
        var sb = new StringBuilder(input.Length);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];

            if (c == '\r')
            {
                if (i + 1 < input.Length && input[i + 1] == '\n')
                    i++;

                sb.Append('\n');
            }
            else if (c == '\f')
                sb.Append('\n');
            else if (c == '\0')
                sb.Append('�');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Processes the input text and returns a set of SUIL tokens extracted from it.
    /// </summary>
    [Pure]
    public static List<SuilToken> Tokenize(string input, string? fileName = null)
    {
        var codePoints = ToCodePoints(Preprocess(input));
        var lines = new LineMap(codePoints);
        var stream = new SuilCodePointStream(codePoints);
        var tokens = new List<SuilToken>();

        try
        {
            while (true)
            {
                var start = stream.Position + 1;
                var consumedComments = ConsumeComments(ref stream);
                var token = consumedComments ? new WhitespaceToken() : ConsumeToken(ref stream);

                if (token is EofToken)
                    break;

                // Comments and the whitespace around them are merged into one token.
                if (token is WhitespaceToken && tokens.Count > 0 && tokens[^1].Value is WhitespaceToken)
                    continue;

                var (line, column) = lines.Locate(start);
                tokens.Add(new SuilToken(token, line, column));
            }
        }
        catch (SuilTokenizeError e)
        {
            (e.Line, e.Column) = lines.Locate(e.Position);
            e.File = fileName;
            throw;
        }

        return tokens;
    }

    private static ISuilToken ConsumeToken(ref SuilCodePointStream stream)
    {
        var cp = stream.ConsumeNext();

        if (IsWhitespace(cp))
        {
            stream.ConsumeWhile(IsWhitespace);
            return new WhitespaceToken();
        }

        switch (cp)
        {
            case 0x0022: // "
                return ConsumeString(ref stream);
            case 0x0023: // #
                if (NameHasVariable(ref stream))
                    return ConsumeInterpolatedName(ref stream, cp);

                if (!IsIdentChar(stream.PeekNext()))
                    return new DelimToken(cp);

                var isId = IsIdentStart(stream.PeekNext());
                return new HashToken(CodePointsToString(stream.ConsumeWhile(IsIdentChar)), isId);
            case 0x0024: // $
                return ConsumeLocKey(ref stream);
            case 0x0025: // %
                return ConsumeContentSlot(ref stream);
            case 0x0028: // (
                return new LeftParenthesisToken();
            case 0x0029: // )
                return new RightParenthesisToken();
            case 0x002B or 0x002D or 0x002E: // + - .
                if (!WouldStartNumber(ref stream))
                {
                    if (cp == 0x002E && NameHasVariable(ref stream))
                        return ConsumeInterpolatedName(ref stream, cp);

                    // a name with a leading hyphen, like -rf-text-outline-width
                    if (cp == 0x002D && IsIdentStart(stream.PeekNext()))
                    {
                        stream.Reconsume();
                        return ConsumeIdentLikeToken(ref stream);
                    }

                    return new DelimToken(cp);
                }

                stream.Reconsume();
                return ConsumeNumericToken(ref stream);
            case 0x002C: // ,
                return new CommaToken();
            case 0x003A: // :
                return new ColonToken();
            case 0x003B: // ;
                return new SemicolonToken();
            case 0x005F: // _
                stream.Reconsume();
                var underscore = CodePointsToString(stream.ConsumeWhile(IsIdentChar));

                if (underscore != "_")
                {
                    throw new SuilTokenizeError("consuming ident",
                        cp,
                        stream.Position,
                        "an ident that doesn't start with an underscore");
                }

                return new UnderscoreToken();
            case 0x0060: // `
                return new VariableToken(ConsumeVariableName(ref stream));
            case 0x007B: // {
                return new LeftCurlyBracket();
            case 0x007D: // }
                return new RightCurlyBracket();
        }

        if (IsDigit(cp))
        {
            stream.Reconsume();
            return ConsumeNumericToken(ref stream);
        }

        if (IsIdentStart(cp))
        {
            stream.Reconsume();
            return ConsumeIdentLikeToken(ref stream);
        }

        if (IsEof(cp))
            return new EofToken();

        return new DelimToken(cp);
    }

    /// <summary>
    /// Consumes a string. The opening quote is already consumed. Besides the escapes the string may
    /// contain <c>`variable`</c> references, so its value is a list of parts.
    /// </summary>
    private static StringToken ConsumeString(ref SuilCodePointStream stream)
    {
        var parts = ImmutableArray.CreateBuilder<StringPart>();
        var text = new StringBuilder();

        while (true)
        {
            var cp = stream.ConsumeNext();

            if (cp == 0x0022) // "
            {
                FlushLiteral(parts, text);
                return new StringToken(parts.ToImmutable());
            }

            if (IsEof(cp) || IsNewline(cp))
                throw new SuilTokenizeError("consuming string", cp, stream.Position, null, 0x0022);

            if (cp == 0x005C) // \
            {
                var next = stream.PeekNext();

                if (IsEof(next))
                    continue;

                if (IsNewline(next))
                {
                    stream.ConsumeNext();
                    continue;
                }

                var escaped = stream.ConsumeNext();
                text.Append(CodePointToString(escaped switch
                {
                    0x006E => 0x000A, // \n
                    0x0074 => 0x0009, // \t
                    _ => escaped,
                }));
                continue;
            }

            if (cp == 0x0060) // `
            {
                FlushLiteral(parts, text);
                parts.Add(StringPart.Variable(ConsumeVariableName(ref stream)));
                continue;
            }

            text.Append(CodePointToString(cp));
        }
    }

    /// <summary>
    /// Whether the name that follows a '.' or '#' has a `variable` reference: ident code points go up to a backtick.
    /// Nothing is consumed.
    /// </summary>
    private static bool NameHasVariable(ref SuilCodePointStream stream)
    {
        for (var offset = 1;; offset++)
        {
            var cp = stream.PeekNext(offset);

            if (cp == 0x0060) // `
                return true;

            if (!IsIdentChar(cp))
                return false;
        }
    }

    /// <summary>
    /// Consumes the name after '.' or '#' (already consumed) that has `variable` references.
    /// </summary>
    private static InterpolatedNameToken ConsumeInterpolatedName(ref SuilCodePointStream stream, int sigil)
    {
        var parts = ImmutableArray.CreateBuilder<StringPart>();
        var text = new StringBuilder();

        while (true)
        {
            var next = stream.PeekNext();

            if (IsIdentChar(next))
            {
                text.Append(CodePointToString(stream.ConsumeNext()));
                continue;
            }

            if (next != 0x0060) // `
                break;

            stream.ConsumeNext();
            FlushLiteral(parts, text);
            parts.Add(StringPart.Variable(ConsumeVariableName(ref stream)));
        }

        FlushLiteral(parts, text);
        return new InterpolatedNameToken(sigil, parts.ToImmutable());
    }

    /// <summary>
    /// Consumes <c>name`</c> after the opening backtick and returns the name.
    /// </summary>
    private static string ConsumeVariableName(ref SuilCodePointStream stream)
    {
        if (!IsIdentStart(stream.PeekNext()))
            throw new SuilTokenizeError("consuming variable", stream.PeekNext(), stream.Position + 1, "a variable name");

        var name = CodePointsToString(stream.ConsumeWhile(IsIdentChar));

        if (stream.PeekNext() != 0x0060) // `
            throw new SuilTokenizeError("consuming variable", stream.PeekNext(), stream.Position + 1, null, 0x0060);

        stream.ConsumeNext();
        return name;
    }

    /// <summary>
    /// Consumes the key after <c>$</c>: ident code points, dots and `variable` references.
    /// </summary>
    private static LocToken ConsumeLocKey(ref SuilCodePointStream stream)
    {
        if (stream.PeekNext() == 0x002E)
        {
            throw new SuilTokenizeError("consuming localization key",
                0x002E,
                stream.Position + 1,
                "a key that doesn't start with a dot (relative keys are not supported)");
        }

        var parts = ImmutableArray.CreateBuilder<StringPart>();
        var text = new StringBuilder();

        while (true)
        {
            var next = stream.PeekNext();

            if (IsLocChar(next))
            {
                text.Append(CodePointToString(stream.ConsumeNext()));
                continue;
            }

            if (next != 0x0060) // `
                break;

            stream.ConsumeNext();
            FlushLiteral(parts, text);
            parts.Add(StringPart.Variable(ConsumeVariableName(ref stream)));
        }

        FlushLiteral(parts, text);

        if (parts.Count == 0)
            throw new SuilTokenizeError("consuming localization key", stream.PeekNext(), stream.Position + 1, "a key");

        var hasArguments = stream.PeekNext() == 0x0028; // (
        if (hasArguments)
            stream.ConsumeNext();

        return new LocToken(parts.ToImmutable(), hasArguments);
    }

    /// <summary>
    /// Consumes <c>name%</c> after <c>%</c>. A percent sign that isn't a part of <c>%name%</c> is a delim.
    /// </summary>
    private static ISuilToken ConsumeContentSlot(ref SuilCodePointStream stream)
    {
        if (!IsIdentStart(stream.PeekNext()))
            return new DelimToken(0x0025);

        var length = 1;
        while (IsIdentChar(stream.PeekNext(length + 1)))
        {
            length++;
        }

        if (stream.PeekNext(length + 1) != 0x0025) // %
            return new DelimToken(0x0025);

        var name = CodePointsToString(stream.PeekNextAmount(length));
        stream.ConsumeNext(length + 1);
        return new ContentSlotToken(name);
    }

    private static ISuilToken ConsumeNumericToken(ref SuilCodePointStream stream)
    {
        var number = ConsumeNumber(ref stream);

        if (IsIdentStart(stream.PeekNext()))
        {
            var unit = CodePointsToString(stream.ConsumeWhile(IsIdentChar));
            return new DimensionToken(number.Value, number.IsInteger, unit);
        }

        if (stream.PeekNext() == 0x0025) // %
        {
            stream.ConsumeNext();
            return new PercentageToken(number.Value);
        }

        return new NumberToken(number.Value, number.IsInteger);
    }

    /// <summary>
    /// Consumes an ident, and a function token if the ident is glued to <c>(</c>.
    /// </summary>
    private static ISuilToken ConsumeIdentLikeToken(ref SuilCodePointStream stream)
    {
        var value = CodePointsToString(stream.ConsumeWhile(IsIdentChar));

        if (stream.PeekNext() == 0x0028) // (
        {
            stream.ConsumeNext();
            return new FunctionToken(value);
        }

        return new IdentToken(value);
    }

    /// <summary>
    /// Consume a number (CSS Syntax §4.3.13). Assumes the caller has already verified that a number follows.
    /// </summary>
    private static ConsumedNumber ConsumeNumber(ref SuilCodePointStream stream)
    {
        var repr = new List<int>();
        var isInteger = true;

        if (stream.PeekNext() is 0x002B or 0x002D) // + or -
            repr.Add(stream.ConsumeNext());

        while (IsDigit(stream.PeekNext()))
        {
            repr.Add(stream.ConsumeNext());
        }

        if (stream.PeekNext() == 0x002E && IsDigit(stream.PeekNext(2))) // . then digit
        {
            repr.Add(stream.ConsumeNext());
            isInteger = false;

            while (IsDigit(stream.PeekNext()))
            {
                repr.Add(stream.ConsumeNext());
            }
        }

        if (stream.PeekNext() is 0x0065 or 0x0045) // e or E
        {
            var hasSign = stream.PeekNext(2) is 0x002B or 0x002D;
            var digitOffset = hasSign ? 3 : 2;

            if (IsDigit(stream.PeekNext(digitOffset)))
            {
                isInteger = false;
                repr.Add(stream.ConsumeNext());
                if (hasSign)
                    repr.Add(stream.ConsumeNext());

                while (IsDigit(stream.PeekNext()))
                {
                    repr.Add(stream.ConsumeNext());
                }
            }
        }

        var reprStr = CodePointsToString(repr);
        var value = double.Parse(reprStr, NumberStyles.Float, CultureInfo.InvariantCulture);

        return new ConsumedNumber(reprStr, isInteger, value);
    }

    /// <summary>
    /// Consumes comments: <c>/* ... */</c> and <c>// ... </c> up to the end of the line (the newline stays).
    /// </summary>
    private static bool ConsumeComments(ref SuilCodePointStream stream)
    {
        var consumedAny = false;

        while (true)
        {
            if (stream.NextIs(0x002F, 0x002F)) // //
            {
                consumedAny = true;
                stream.ConsumeNext(2);
                stream.ConsumeWhile(cp => !IsEof(cp) && !IsNewline(cp));
                continue;
            }

            if (!stream.NextIs(0x002F, 0x002A)) // /*
                return consumedAny;

            consumedAny = true;
            stream.ConsumeNext(2);

            while (true)
            {
                if (stream.NextIs(0x002A, 0x002F)) // */
                {
                    stream.ConsumeNext(2);
                    break;
                }

                if (IsEof(stream.ConsumeNext()))
                    throw new SuilTokenizeError("consuming comment", -1, stream.Position, null, 0x002A, 0x002F);
            }
        }
    }

    private static void FlushLiteral(ImmutableArray<StringPart>.Builder parts, StringBuilder text)
    {
        if (text.Length == 0)
            return;

        parts.Add(StringPart.Literal(text.ToString()));
        text.Clear();
    }

    private static string CodePointsToString(IEnumerable<int> codePoints)
    {
        var sb = new StringBuilder();
        foreach (var cp in codePoints)
        {
            sb.Append(CodePointToString(cp));
        }
        return sb.ToString();
    }

    /// <summary>
    /// "Starts with a number" as called on the stream itself: the current (already consumed) code point
    /// plus the next two.
    /// </summary>
    private static bool WouldStartNumber(ref SuilCodePointStream stream)
        => WouldStartNumber(stream.Current, stream.PeekNext(), stream.PeekNext(2));

    private static bool WouldStartNumber(int cp1, int cp2, int cp3)
    {
        return cp1 switch
        {
            0x002B or 0x002D when IsDigit(cp2) => true, // +digit/-digit
            0x002B or 0x002D => cp2 == 0x002E && IsDigit(cp3), // -.digit/+.digit
            0x002E when IsDigit(cp2) => true, // .digit
            _ => IsDigit(cp1),
        };
    }
}
