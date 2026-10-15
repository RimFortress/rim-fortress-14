using Content.Suil;
using Content.Client._RF.CSS.Text;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// Parses <c>font-family</c>: a comma-separated list of quoted names and unquoted identifier sequences
/// (<c>"Noto Sans", Arial, sans-serif</c>).
/// </summary>
[UsedImplicitly]
public sealed class CssFontFamilyParser : SuilValueParser<CssFontFamily>
{
    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out CssFontFamily value)
    {
        value = default!;
        var names = new List<string>();
        var words = new List<string>();

        while (!stream.AtEnd)
        {
            switch (stream.Peek())
            {
                case SuilString str:
                    Flush();
                    names.Add(str.Text);
                    stream.ConsumeNext();
                    break;
                case SuilIdent ident:
                    words.Add(ident.Value);
                    stream.ConsumeNext();
                    break;
                case SuilComma:
                    Flush();
                    stream.ConsumeNext();
                    break;
                default:
                    return false;
            }
        }

        Flush();

        if (names.Count == 0)
            return false;

        value = new CssFontFamily(names);
        return true;

        void Flush()
        {
            if (words.Count == 0)
                return;

            names.Add(string.Join(' ', words));
            words.Clear();
        }
    }
}

/// <summary>
/// Parses <c>font-weight</c>: <c>normal</c>, <c>bold</c> or a number from 1 to 1000.
/// <c>lighter</c>/<c>bolder</c> need the parent's weight and are not supported.
/// </summary>
/// <remarks>
/// Not a default parser: <see cref="FontWeight"/> is an enum, so it must be attached with
/// <c>[SuilProperty(parser: typeof(CssFontWeightParser))]</c>.
/// </remarks>
[UsedImplicitly]
public sealed class CssFontWeightParser : SuilValueParser<FontWeight>
{
    public override bool Default => false;

    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out FontWeight value)
    {
        value = FontWeight.Normal;

        switch (stream.Peek())
        {
            case SuilIdent { Value: var ident }:
                if (ident.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    value = FontWeight.Normal;
                else if (ident.Equals("bold", StringComparison.OrdinalIgnoreCase))
                    value = FontWeight.Bold;
                else
                    return false;

                stream.ConsumeNext();
                return true;
            case SuilNumber number:
                var weight = (int) number.Value;

                if (weight is < 1 or > 1000)
                    return false;

                value = (FontWeight) (ushort) weight;
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Parses <c>font-size</c>: a length-percentage, an absolute-size keyword (<c>xx-small</c> ... <c>xxx-large</c>,
/// multiples of <see cref="CssControl.DefaultFontSize"/>) or <c>larger</c>/<c>smaller</c> (<c>1.2em</c>/<c>0.83em</c>).
/// </summary>
/// <remarks>
/// Not a default parser, so it never competes with the generic <see cref="LengthUnit"/> parser. Attach it with
/// <c>[SuilProperty(parser: typeof(CssFontSizeParser))]</c>.
/// </remarks>
[UsedImplicitly]
public sealed class CssFontSizeParser : SuilValueParser<LengthUnit>
{
    public override bool Default => false;

    /// <summary>
    /// Parses a font size without requiring the stream to end. Lets the <c>font</c> shorthand reuse the grammar.
    /// </summary>
    /// <param name="stream">CSS values stream.</param>
    /// <param name="css">The manager that parses plain lengths.</param>
    /// <param name="value">The parsed size.</param>
    public static bool TryParseSize(ref SuilValueStream stream, ISuilValues parsers, out LengthUnit value)
    {
        if (stream.Peek() is SuilIdent { Value: var ident } && TryGetKeyword(ident, out value))
        {
            stream.ConsumeNext();
            return true;
        }

        return parsers.TryParse(ref stream, out value);
    }

    private static bool TryGetKeyword(string ident, out LengthUnit value)
    {
        float? scale = ident.ToLowerInvariant() switch
        {
            "xx-small" => 0.5625f,
            "x-small" => 0.625f,
            "small" => 0.8125f,
            "medium" => 1f,
            "large" => 1.125f,
            "x-large" => 1.5f,
            "xx-large" => 2f,
            "xxx-large" => 3f,
            _ => null,
        };

        if (scale is { } factor)
        {
            value = LengthUnit.Px(factor * CssControl.DefaultFontSize);
            return true;
        }

        switch (ident.ToLowerInvariant())
        {
            case "larger":
                value = LengthUnit.Em(1.2f);
                return true;
            case "smaller":
                value = LengthUnit.Em(1f / 1.2f);
                return true;
            default:
                value = LengthUnit.Zero;
                return false;
        }
    }

    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out LengthUnit value)
        => TryParseSize(ref stream, Values, out value);
}

/// <summary>
/// Parses <c>line-height</c>: <c>normal</c>, a number, a percentage (as a multiplier), or a length in any unit.
/// </summary>
[UsedImplicitly]
public sealed class CssLineHeightParser : SuilValueParser<CssLineHeight>
{
    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out CssLineHeight value)
    {
        value = CssLineHeight.Normal;

        switch (stream.Peek())
        {
            case SuilIdent { Value: var ident } when ident.Equals("normal", StringComparison.OrdinalIgnoreCase):
                stream.ConsumeNext();
                return true;
            case SuilNumber number:
                value = CssLineHeight.FromMultiplier((float) number.Value);
                stream.ConsumeNext();
                return true;
            case SuilPercentage percent:
                value = CssLineHeight.FromMultiplier((float) percent.Value / 100f);
                stream.ConsumeNext();
                return true;
        }

        if (!Values.TryParse<LengthUnit>(ref stream, out var length))
            return false;

        value = CssLineHeight.FromLength(length);
        return true;
    }
}

/// <summary>
/// Parses <c>line-clamp</c>: <c>none</c> (returns 0) or a positive integer.
/// </summary>
/// <remarks>
/// Not a default parser, so it never competes with a generic <see cref="int"/> parser. Attach it with
/// <c>[SuilProperty(parser: typeof(CssLineClampParser))]</c>.
/// </remarks>
[UsedImplicitly]
public sealed class CssLineClampParser : SuilValueParser<int>
{
    public override bool Default => false;

    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out int value)
    {
        value = 0;

        switch (stream.Peek())
        {
            case SuilIdent { Value: var ident } when ident.Equals("none", StringComparison.OrdinalIgnoreCase):
                stream.ConsumeNext();
                return true;
            case SuilNumber number when number.Value >= 1:
                value = (int) number.Value;
                stream.ConsumeNext();
                return true;
            default:
                return false;
        }
    }
}

/// <summary>
/// Parses <c>text-shadow</c>: <c>none</c> or a comma-separated list of
/// <c>offset-x offset-y [blur] [color]</c> layers (the color may also come first).
/// </summary>
[UsedImplicitly]
public sealed class CssTextShadowParser : SuilValueParser<CssTextShadow[]>
{
    /// <inheritdoc/>
    public override bool TryParse(ref SuilValueStream stream, out CssTextShadow[] value)
    {
        value = Array.Empty<CssTextShadow>();

        if (stream.Peek() is SuilIdent { Value: var ident } && ident.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            stream.ConsumeNext();
            value = [];
            return true;
        }

        var layers = new List<CssTextShadow>();

        while (true)
        {
            var lengths = new List<LengthUnit>(3);
            Color? color = null;

            while (!stream.AtEnd && stream.Peek() is not SuilComma)
            {
                var mark = stream.Mark();

                if (lengths.Count < 3 && Values.TryParse<LengthUnit>(ref stream, out var length))
                {
                    lengths.Add(length);
                    continue;
                }

                stream.Reset(mark);

                if (color == null && Values.TryParse<Color>(ref stream, out var parsed))
                {
                    color = parsed;
                    continue;
                }

                return false;
            }

            if (lengths.Count is not (2 or 3))
                return false;

            layers.Add(new CssTextShadow(new UnitVector2(lengths[0], lengths[1]), lengths.Count == 3 ? lengths[2] : LengthUnit.Zero, color));

            if (stream.AtEnd)
                break;

            stream.ConsumeNext(); // the comma
        }

        value = layers.ToArray();
        return true;
    }
}
