using Content.Suil;
using Content.Client._RF.Suil.Text;
using JetBrains.Annotations;

namespace Content.Client._RF.Suil.Parsers;

/// <summary>
/// Parses <c>text-decoration-line</c>: <c>none | [ underline || overline || line-through ]</c>.
/// </summary>
/// <remarks>
/// Enums never reach the registered parsers (<see cref="SuilManager"/> sends every enum type to its generic
/// single-ident enum parser), so this parser is not a default one and has to be attached explicitly:
/// <c>[SuilProperty(parser: typeof(SuilTextDecorationLineParser))]</c>.
/// <c>blink</c> is not supported and makes the value invalid.
/// </remarks>
[UsedImplicitly]
public sealed class SuilTextDecorationLineParser : SuilValueParser<SuilTextDecorationLine>
{
    public override bool Default => false;

    /// <summary>
    /// Maps a single keyword to its flag. <c>none</c> maps to <see cref="SuilTextDecorationLine.None"/>.
    /// </summary>
    /// <param name="ident">The identifier text (case-insensitive).</param>
    /// <param name="flag">The resulting flag.</param>
    /// <returns><c>false</c> if the identifier is not a text-decoration-line keyword.</returns>
    public static bool TryParseKeyword(string ident, out SuilTextDecorationLine flag)
    {
        switch (ident.ToLowerInvariant())
        {
            case "none":
                flag = SuilTextDecorationLine.None;
                return true;
            case "underline":
                flag = SuilTextDecorationLine.Underline;
                return true;
            case "overline":
                flag = SuilTextDecorationLine.Overline;
                return true;
            case "line-through":
                flag = SuilTextDecorationLine.LineThrough;
                return true;
            default:
                flag = SuilTextDecorationLine.None;
                return false;
        }
    }

    /// <summary>
    /// Consumes as many line keywords as the stream starts with, without requiring the stream to end.
    /// Lets the <c>text-decoration</c> shorthand reuse the grammar.
    /// </summary>
    /// <param name="stream">SUIL values stream.</param>
    /// <param name="value">The combined flags.</param>
    /// <returns>
    /// <c>false</c> if no keyword was consumed, a keyword was repeated,
    /// or <c>none</c> was combined with other keywords.
    /// </returns>
    public static bool TryParseLine(ref SuilValueStream stream, out SuilTextDecorationLine value)
    {
        value = SuilTextDecorationLine.None;

        if (stream.Peek() is SuilIdent { Value: var first }
            && first.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            stream.ConsumeNext();
            return true;
        }

        var seen = false;

        while (stream.Peek() is SuilIdent { Value: var ident }
               && TryParseKeyword(ident, out var flag)
               && flag != SuilTextDecorationLine.None)
        {
            if ((value & flag) != 0)
            {
                value = SuilTextDecorationLine.None;
                return false;
            }

            value |= flag;
            stream.ConsumeNext();
            seen = true;
        }

        return seen;
    }

    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out SuilTextDecorationLine value)
        => TryParseLine(ref stream, out value) && stream.AtEnd;
}
