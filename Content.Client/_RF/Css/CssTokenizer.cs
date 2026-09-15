using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace Content.Client._RF.Css;

/// <summary>
/// CCS Text Tokenizer. Written entirely in accordance with the CSS Syntax Module Level 3 specification,
/// with one exception: this tokenizer has zero tolerance for syntax errors
/// and will return a <see cref="CssTokenizeError"/> if any are found.
/// </summary>
public static class CssTokenizer
{
    /// <summary>
    /// A conceptual code point representing the end of the input stream.
    /// Whenever the input stream is empty, the next input code point is always an EOF code point.
    /// </summary>
    private static bool IsEof(int cp) => cp == -1;

    /// <summary>
    /// A code point between U+0030 DIGIT ZERO (0) and U+0039 DIGIT NINE (9) inclusive.
    /// </summary>
    private static bool IsDigit(int cp) => cp is >= 0x0030 and <= 0x0039;

    /// <summary>
    /// A digit, or a code point between U+0041 LATIN CAPITAL LETTER A (A)
    /// and U+0046 LATIN CAPITAL LETTER F (F) inclusive, or a code point between U+0061 LATIN SMALL LETTER A (a)
    /// and U+0066 LATIN SMALL LETTER F (f) inclusive.
    /// </summary>
    private static bool IsHexDigit(int cp) => IsDigit(cp) || cp is >= 0x0041 and <= 0x0046 or >= 0x0061 and <= 0x0066;

    /// <summary>
    /// A code point between U+0041 LATIN CAPITAL LETTER A (A) and U+005A LATIN CAPITAL LETTER Z (Z) inclusive.
    /// </summary>
    private static bool IsUppercaseLetter(int cp) => cp is >= 0x0041 and <= 0x005A;

    /// <summary>
    /// A code point between U+0061 LATIN SMALL LETTER A (a) and U+007A LATIN SMALL LETTER Z (z) inclusive.
    /// </summary>
    private static bool IsLowercaseLetter(int cp) => cp is >= 0x0061 and <= 0x007A;

    /// <summary>
    /// An uppercase letter or a lowercase letter.
    /// </summary>
    private static bool IsLetter(int cp) => IsUppercaseLetter(cp) || IsLowercaseLetter(cp);

    /// <summary>
    /// A code point with a value equal to or greater than U+0080 control.
    /// </summary>
    private static bool IsNonAscii(int cp) => cp >= 0x0080;

    /// <summary>
    /// A letter, a non-ASCII code point, or U+005F LOW LINE (_).
    /// </summary>
    private static bool IsIdentStart(int cp) => IsLetter(cp) || IsNonAscii(cp) || cp == 0x005F;

    /// <summary>
    /// An ident-start code point, a digit, or U+002D HYPHEN-MINUS (-).
    /// </summary>
    private static bool IsIdent(int cp) => IsIdentStart(cp) || IsDigit(cp) || cp == 0x002D;

    /// <summary>
    /// A code point between U+0000 NULL and U+0008 BACKSPACE inclusive, or U+000B LINE TABULATION,
    /// or a code point between U+000E SHIFT OUT and U+001F INFORMATION SEPARATOR ONE inclusive, or U+007F DELETE.
    /// </summary>
    private static bool IsNonPrintable(int cp)
        => cp is >= 0x0000 and <= 0x0008 or 0x000B or >= 0x000E and <= 0x001F or 0x007F;

    /// <summary>
    /// U+000A LINE FEED.
    /// </summary>
    /// <remarks>
    /// Note that U+000D CARRIAGE RETURN and U+000C FORM FEED are not included in this
    /// definition, as they are converted to U+000A LINE FEED during preprocessing.
    /// </remarks>
    private static bool IsNewline(int cp) => cp == 0x000A;

    /// <summary>
    /// A newline, U+0009 CHARACTER TABULATION, or U+0020 SPACE.
    /// </summary>
    private static bool IsWhitespace(int cp) => IsNewline(cp) || cp is 0x0009 or 0x0020;

    /// <summary>
    /// The greatest code point defined by Unicode: U+10FFFF.
    /// </summary>
    private static bool IsMaximumAllowed(int cp) => cp == 0x10FFFF;

    private static bool IsSurrogate(int cp) => cp is >= 0xD800 and <= 0xDFFF;

    private readonly record struct ConsumedNumber(string Representation, bool IsInteger, double Value);

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
                sb.Append('\uFFFD');
            else
                sb.Append(c);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Processes the input text and returns a set of CSS tokens extracted from it.
    /// </summary>
    [PublicAPI, Pure]
    public static List<ICssToken> Tokenize(string input)
    {
        var codePoints = Preprocess(input).EnumerateRunes().Select(r => r.Value).ToArray();
        var stream = new CodePointStream(codePoints);
        var tokens = new List<ICssToken>();

        while (true)
        {
            var token = ConsumeToken(ref stream);

            if (token is EofToken)
                break;

            if (token == null)
                continue;

            tokens.Add(token);
        }

        return tokens;
    }

    private static ICssToken? ConsumeToken(ref CodePointStream stream)
    {
        if (ConsumeComments(ref stream))
            return null;

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
                var next1 = stream.PeekNext();
                var next2 = stream.PeekNext(2);

                if (!IsIdent(next1) && !TwoCodePointsAreValidEscape(next1, next2))
                    return new DelimToken(cp);

                var next3 = stream.PeekNext(3);
                var isId = WouldStartIdentSequence(next1, next2, next3);

                var value = ConsumeIdentSequence(ref stream);
                return new HashToken(value, isId);
            case 0x0027: // '
                return ConsumeString(ref stream);
            case 0x0028: // (
                return new LeftParenthesisToken();
            case 0x0029: // (
                return new RightParenthesisToken();
            case 0x002B: // +
                if (!WouldStartNumber(ref stream))
                    return new DelimToken(cp);

                stream.Reconsume();
                return ConsumeNumericToken(ref stream);
            case 0x002C: // ,
                return new CommaToken();
            case 0x002D: // -
                if (WouldStartNumber(ref stream))
                {
                    stream.Reconsume();
                    return ConsumeNumericToken(ref stream);
                }

                if (stream.NextIs(0x002D, 0x003E)) // ->
                {
                    stream.ConsumeNext(2);
                    return new CdcToken();
                }

                if (WouldStartIdentSequenceFromCurrent(ref stream))
                {
                    stream.Reconsume();
                    return ConsumeIdentLikeToken(ref stream);
                }

                return new DelimToken(cp);
            case 0x002E: // .
                if (!WouldStartNumber(ref stream))
                    return new DelimToken(cp);

                stream.Reconsume();
                return ConsumeNumericToken(ref stream);
            case 0x003A: // :
                return new ColonToken();
            case 0x003B: // ;
                return new SemicolonToken();
            case 0x003C: // <
                if (stream.NextIs(0x0021, 0x002D, 0x002D))
                {
                    stream.ConsumeNext(3);
                    return new CdoToken();
                }

                return new DelimToken(cp);
            case 0x0040: // @
                if (WouldStartIdentSequenceFromNext(ref stream))
                    return new AtKeywordToken(ConsumeIdentSequence(ref stream));

                return new DelimToken(cp);
            case 0x005B: // [
                return new LeftSquareBracket();
            case 0x005C: // \
                if (TwoCodePointsAreValidEscape(cp, stream.PeekNext()))
                {
                    stream.Reconsume();
                    return ConsumeIdentLikeToken(ref stream);
                }

                throw new CssTokenizeError("consuming escape ident like token", cp, "valid ident like token");
            case 0x005D: // ]
                return new RightSquareBracket();
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

    private static StringToken ConsumeString(ref CodePointStream stream, int? endCp = null)
    {
        endCp ??= stream.Current;
        var value = new List<int>();

        while (true)
        {
            var cp = stream.ConsumeNext();

            if (cp == endCp)
                return new StringToken(CodePointsToString(value));

            if (IsEof(cp) || IsNewline(cp))
                throw new CssTokenizeError("consuming string", stream.Current, null, endCp.Value);

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

                var escaped = ConsumeEscapedCodePoint(ref stream);
                value.Add(escaped);
                continue;
            }

            value.Add(cp);
        }
    }

    private static ICssToken ConsumeNumericToken(ref CodePointStream stream)
    {
        var number = ConsumeNumber(ref stream);

        if (WouldStartIdentSequenceFromNext(ref stream))
        {
            var unit = ConsumeIdentSequence(ref stream);
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
    /// Consume an ident-like token (§4.3.4). Assumes the stream is positioned
    /// such that the next code points form an ident sequence (i.e. this is called
    /// after "would start an ident sequence" returned true for the current position).
    /// https://www.w3.org/TR/css-syntax-3/#consume-an-ident-like-token
    /// </summary>
    private static ICssToken ConsumeIdentLikeToken(ref CodePointStream stream)
    {
        var value = ConsumeIdentSequence(ref stream);

        // special-case: url(...) has its own consumption rules, distinct from a regular function
        if (value.Equals("url", StringComparison.OrdinalIgnoreCase) && stream.PeekNext() == 0x0028) // (
        {
            stream.ConsumeNext(); // consume '('

            // consume as much whitespace as possible
            while (IsWhitespace(stream.PeekNext()))
            {
                stream.ConsumeNext();
            }

            // if a quote follows (immediately, since whitespace is already exhausted),
            // this is a quoted url — falls through to regular string-token parsing
            // as function arguments, so treat it as a function-token instead
            if (stream.PeekNext() is 0x0022 or 0x0027) // " or '
                return new FunctionToken(value);

            return ConsumeUrlToken(ref stream);
        }

        if (stream.PeekNext() == 0x0028) // (
        {
            stream.ConsumeNext();
            return new FunctionToken(value);
        }

        return new IdentToken(value);
    }
    /// <summary>
    /// Consume a number (§4.3.13). Assumes the caller has already verified
    /// (via "three code points would start a number") that a number follows.
    /// </summary>
    private static ConsumedNumber ConsumeNumber(ref CodePointStream stream)
    {
        var repr = new List<int>();
        var isInteger = true;

        // 1. optional sign
        if (stream.PeekNext() is 0x002B or 0x002D) // + or -
            repr.Add(stream.ConsumeNext());

        // 2. integer part
        while (IsDigit(stream.PeekNext()))
        {
            repr.Add(stream.ConsumeNext());
        }

        // 3. fractional part: "." followed by a digit
        if (stream.PeekNext() == 0x002E && IsDigit(stream.PeekNext(2))) // . then digit
        {
            repr.Add(stream.ConsumeNext()); // the '.'
            isInteger = false;

            while (IsDigit(stream.PeekNext()))
            {
                repr.Add(stream.ConsumeNext());
            }
        }

        // 4. exponent: e/E, optional sign, then digit(s)
        if (stream.PeekNext() is 0x0065 or 0x0045) // e or E
        {
            var signOffset = 2;
            var hasSign = stream.PeekNext(2) is 0x002B or 0x002D;
            if (hasSign)
                signOffset = 3;

            if (IsDigit(stream.PeekNext(signOffset)))
            {
                isInteger = false;
                repr.Add(stream.ConsumeNext()); // e/E
                if (hasSign)
                    repr.Add(stream.ConsumeNext()); // + or -

                while (IsDigit(stream.PeekNext()))
                {
                    repr.Add(stream.ConsumeNext());
                }
            }
        }

        var reprStr = CodePointsToString(repr.ToArray());
        var value = ConvertStringToNumber(reprStr);

        return new ConsumedNumber(reprStr, isInteger, value);
    }

    private static bool ConsumeComments(ref CodePointStream stream)
    {
        var consumedAny = false;

        while (stream.NextIs(0x002F, 0x002A)) // /*
        {
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
                    throw new CssTokenizeError("consuming comment", stream.Current, null, 0x002A, 0x002F);
            }
        }

        return consumedAny;
    }

    /// <summary>
    /// Consume an escaped code point. Assumes the U+005C (backslash) has already been consumed
    /// and that the stream does not start with a newline or EOF.
    /// </summary>
    private static int ConsumeEscapedCodePoint(ref CodePointStream stream)
    {
        var cp = stream.ConsumeNext();

        if (IsEof(cp))
            throw new CssTokenizeError("consuming escape code point", cp, "escape code point");

        if (!IsHexDigit(cp))
            return cp; // anything else — return as-is

        var hexDigits = new List<int> { cp };

        while (hexDigits.Count < 6 && IsHexDigit(stream.PeekNext()))
        {
            hexDigits.Add(stream.ConsumeNext());
        }

        // consume one trailing whitespace, if present
        if (IsWhitespace(stream.PeekNext()))
            stream.ConsumeNext();

        var value = Convert.ToInt32(CodePointsToString(hexDigits), 16);

        if (value == 0 || IsSurrogate(value) || IsMaximumAllowed(value))
            return 0xFFFD;

        return value;
    }

    /// <summary>
    /// Consume an ident sequence (§4.3.12). Repeatedly consumes ident code points,
    /// resolving escapes along the way, until a non-ident, non-escape code point is found.
    /// </summary>
    private static string ConsumeIdentSequence(ref CodePointStream stream)
    {
        var result = new List<int>();

        while (true)
        {
            var cp = stream.ConsumeNext();

            if (IsIdent(cp))
            {
                result.Add(cp);
                continue;
            }

            if (TwoCodePointsAreValidEscape(cp, stream.PeekNext()))
            {
                result.Add(ConsumeEscapedCodePoint(ref stream));
                continue;
            }

            stream.Reconsume();
            return CodePointsToString(result);
        }
    }

    /// <summary>
    /// Consume a url token (§4.3.6). Assumes "url(" and any leading whitespace
    /// inside the parens have already been consumed by the caller.
    /// https://www.w3.org/TR/css-syntax-3/#consume-a-url-token
    /// </summary>
    private static UrlToken ConsumeUrlToken(ref CodePointStream stream)
    {
        var value = new List<int>();

        while (true)
        {
            var cp = stream.ConsumeNext();

            if (cp == 0x0029) // (
                return new UrlToken(CodePointsToString(value.ToArray()));

            if (IsEof(cp))
                throw new CssTokenizeError("consuming url", stream.Current, null, 0x0029);

            if (IsWhitespace(cp))
            {
                while (IsWhitespace(stream.PeekNext()))
                {
                    stream.ConsumeNext();
                }

                var next = stream.ConsumeNext();

                if (next == 0x0029) // )
                    return new UrlToken(CodePointsToString(value.ToArray()));

                if (IsEof(next))
                    throw new CssTokenizeError("consuming url", stream.Current, null, 0x0029);

                // anything else after whitespace inside an unquoted url() is invalid,
                // e.g. url(foo bar) — no bad-url recovery, just fail hard
                throw new CssTokenizeError("consuming url", stream.Current, "valid token after whitespace");
            }

            if (cp is 0x0022 or 0x0027 or 0x0028 || IsNonPrintable(cp)) // " ' (
                throw new CssTokenizeError("consuming url", stream.Current, "valid url character");

            if (cp == 0x005C) // \
            {
                if (TwoCodePointsAreValidEscape(cp, stream.PeekNext()))
                {
                    value.Add(ConsumeEscapedCodePoint(ref stream));
                    continue;
                }

                throw new CssTokenizeError("consuming url", stream.Current, "valid escape sequence");
            }

            value.Add(cp);
        }
    }

    private static string CodePointsToString(int[] codePoints)
    {
        var sb = new StringBuilder(codePoints.Length);
        foreach (var cp in codePoints)
        {
            sb.Append(new Rune(cp));
        }
        return sb.ToString();
    }

    private static string CodePointsToString(List<int> codePoints)
    {
        var sb = new StringBuilder(codePoints.Count);
        foreach (var cp in codePoints)
        {
            sb.Append(new Rune(cp));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Check if two code points are a valid escape (§4.3.8). Does not consume anything.
    /// </summary>
    private static bool TwoCodePointsAreValidEscape(int first, int second)
    {
        if (first != 0x005C) // \
            return false;

        return !IsNewline(second);
    }

    /// <summary>
    /// "Starts with a number" as called on the stream itself (§4.3.10): uses the current
    /// input code point (already consumed) plus the next two as the three-code-point window.
    /// </summary>
    private static bool WouldStartNumber(ref CodePointStream stream)
        => WouldStartNumber(stream.Current, stream.PeekNext(), stream.PeekNext(2));

    /// <summary>
    /// "Starts with an ident sequence" as called on the stream itself (§4.3.9): uses the
    /// current input code point (already consumed) plus the next two. Used where the spec
    /// says "the input stream starts with an ident sequence" (the U+002D branch).
    /// </summary>
    private static bool WouldStartIdentSequenceFromCurrent(ref CodePointStream stream)
        => WouldStartIdentSequence(stream.Current, stream.PeekNext(), stream.PeekNext(2));

    /// <summary>
    /// "The next 3 input code points would start an ident sequence" (explicit "next 3"
    /// wording in the spec) — used for U+0040 and inside "consume a numeric token", where
    /// the current code point is deliberately excluded from the window.
    /// </summary>
    private static bool WouldStartIdentSequenceFromNext(ref CodePointStream stream)
        => WouldStartIdentSequence(stream.PeekNext(), stream.PeekNext(2), stream.PeekNext(3));

    /// <summary>
    /// Check if three code points would start an ident sequence (§4.3.9). Does not consume anything.
    /// </summary>
    private static bool WouldStartIdentSequence(int first, int second, int third)
    {
        if (first == 0x002D) // -
        {
            if (IsIdentStart(second) || second == 0x002D)
                return true;

            return TwoCodePointsAreValidEscape(second, third);
        }

        if (IsIdentStart(first))
            return true;

        return first == 0x005C && TwoCodePointsAreValidEscape(first, second);
    }

    /// <summary>
    /// Check if three code points would start a number.
    /// https://www.w3.org/TR/css-syntax-3/#check-if-three-code-points-would-start-a-number
    /// </summary>
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

    private static double ConvertStringToNumber(string repr) => double.Parse(repr,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture);
}
