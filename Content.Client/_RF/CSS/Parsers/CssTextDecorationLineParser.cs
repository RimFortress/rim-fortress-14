using Content.Client._RF.CSS.Streams;
using Content.Client._RF.CSS.Text;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// Parses <c>text-decoration-line</c>: <c>none | [ underline || overline || line-through ]</c>.
/// </summary>
/// <remarks>
/// Enums never reach the registered parsers (<see cref="CssManager"/> sends every enum type to its generic
/// single-ident enum parser), so this parser is not a default one and has to be attached explicitly:
/// <c>[CssProperty(parser: typeof(CssTextDecorationLineParser))]</c>.
/// <c>blink</c> is not supported and makes the value invalid.
/// </remarks>
[UsedImplicitly]
public sealed class CssTextDecorationLineParser : CssValueParser<CssTextDecorationLine>
{
    public override bool Default => false;

    /// <summary>
    /// Maps a single keyword to its flag. <c>none</c> maps to <see cref="CssTextDecorationLine.None"/>.
    /// </summary>
    /// <param name="ident">The identifier text (case-insensitive).</param>
    /// <param name="flag">The resulting flag.</param>
    /// <returns><c>false</c> if the identifier is not a text-decoration-line keyword.</returns>
    public static bool TryParseKeyword(string ident, out CssTextDecorationLine flag)
    {
        switch (ident.ToLowerInvariant())
        {
            case "none":
                flag = CssTextDecorationLine.None;
                return true;
            case "underline":
                flag = CssTextDecorationLine.Underline;
                return true;
            case "overline":
                flag = CssTextDecorationLine.Overline;
                return true;
            case "line-through":
                flag = CssTextDecorationLine.LineThrough;
                return true;
            default:
                flag = CssTextDecorationLine.None;
                return false;
        }
    }

    /// <summary>
    /// Consumes as many line keywords as the stream starts with, without requiring the stream to end.
    /// Lets the <c>text-decoration</c> shorthand reuse the grammar.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="value">The combined flags.</param>
    /// <returns>
    /// <c>false</c> if no keyword was consumed, a keyword was repeated,
    /// or <c>none</c> was combined with other keywords.
    /// </returns>
    public static bool TryParseLine(ref CssValueStream stream, out CssTextDecorationLine value)
    {
        value = CssTextDecorationLine.None;

        if (stream.Peek() is IdentToken { Value: var first }
            && first.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            stream.ConsumeNext();
            return true;
        }

        var seen = false;

        while (stream.Peek() is IdentToken { Value: var ident }
               && TryParseKeyword(ident, out var flag)
               && flag != CssTextDecorationLine.None)
        {
            if ((value & flag) != 0)
            {
                value = CssTextDecorationLine.None;
                return false;
            }

            value |= flag;
            stream.ConsumeNext();
            seen = true;
        }

        return seen;
    }

    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out CssTextDecorationLine value)
        => TryParseLine(ref stream, out value) && stream.AtEnd;
}
