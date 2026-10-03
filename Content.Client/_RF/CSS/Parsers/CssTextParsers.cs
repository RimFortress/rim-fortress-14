using System.Numerics;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.CSS.Text;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// Parses <c>font-family</c>: a comma-separated list of quoted names and unquoted identifier sequences
/// (<c>"Noto Sans", Arial, sans-serif</c>).
/// </summary>
[UsedImplicitly]
public sealed class CssFontFamilyParser : CssValueParser<CssFontFamily>
{
    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out CssFontFamily value)
    {
        value = default!;
        var names = new List<string>();
        var words = new List<string>();

        while (!stream.AtEnd)
        {
            switch (stream.Peek())
            {
                case StringToken str:
                    Flush();
                    names.Add(str.Value);
                    stream.ConsumeNext();
                    break;
                case IdentToken ident:
                    words.Add(ident.Value);
                    stream.ConsumeNext();
                    break;
                case CommaToken:
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
/// <c>[CssProperty(parser: typeof(CssFontWeightParser))]</c>.
/// </remarks>
[UsedImplicitly]
public sealed class CssFontWeightParser : CssValueParser<FontWeight>
{
    public override bool Default => false;

    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out FontWeight value)
    {
        value = FontWeight.Normal;

        switch (stream.Peek())
        {
            case IdentToken { Value: var ident }:
                if (ident.Equals("normal", StringComparison.OrdinalIgnoreCase))
                    value = FontWeight.Normal;
                else if (ident.Equals("bold", StringComparison.OrdinalIgnoreCase))
                    value = FontWeight.Bold;
                else
                    return false;

                stream.ConsumeNext();
                return true;
            case NumberToken number:
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
/// Parses <c>line-height</c>: <c>normal</c>, a number, a percentage (as a multiplier), or a length.
/// </summary>
[UsedImplicitly]
public sealed class CssLineHeightParser : CssValueParser<CssLineHeight>
{
    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out CssLineHeight value)
    {
        value = CssLineHeight.Normal;

        switch (stream.Peek())
        {
            case IdentToken { Value: var ident } when ident.Equals("normal", StringComparison.OrdinalIgnoreCase):
                stream.ConsumeNext();
                return true;
            case NumberToken number:
                value = CssLineHeight.FromMultiplier((float) number.Value);
                stream.ConsumeNext();
                return true;
            case PercentageToken percent:
                value = CssLineHeight.FromMultiplier((float) percent.Value / 100f);
                stream.ConsumeNext();
                return true;
        }

        if (!Css.TryParseValue<float>(ref stream, out var length))
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
/// <c>[CssProperty(parser: typeof(CssLineClampParser))]</c>.
/// </remarks>
[UsedImplicitly]
public sealed class CssLineClampParser : CssValueParser<int>
{
    public override bool Default => false;

    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out int value)
    {
        value = 0;

        switch (stream.Peek())
        {
            case IdentToken { Value: var ident } when ident.Equals("none", StringComparison.OrdinalIgnoreCase):
                stream.ConsumeNext();
                return true;
            case NumberToken number when number.Value >= 1:
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
public sealed class CssTextShadowParser : CssValueParser<CssTextShadow[]>
{
    /// <inheritdoc/>
    public override bool TryParse(ref CssValueStream stream, out CssTextShadow[] value)
    {
        value = Array.Empty<CssTextShadow>();

        if (stream.Peek() is IdentToken { Value: var ident } && ident.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            stream.ConsumeNext();
            value = [];
            return true;
        }

        var layers = new List<CssTextShadow>();

        while (true)
        {
            var lengths = new List<float>(3);
            Color? color = null;

            while (!stream.AtEnd && stream.Peek() is not CommaToken)
            {
                var mark = stream.Mark();

                if (lengths.Count < 3 && Css.TryParseValue<float>(ref stream, out var length))
                {
                    lengths.Add(length);
                    continue;
                }

                stream.Reset(mark);

                if (color == null && Css.TryParseValue<Color>(ref stream, out var parsed))
                {
                    color = parsed;
                    continue;
                }

                return false;
            }

            if (lengths.Count is not (2 or 3))
                return false;

            layers.Add(new CssTextShadow(
                new Vector2(lengths[0], lengths[1]),
                lengths.Count == 3 ? lengths[2] : 0f,
                color ?? Color.Black));

            if (stream.AtEnd)
                break;

            stream.ConsumeNext(); // the comma
        }

        value = layers.ToArray();
        return true;
    }
}
