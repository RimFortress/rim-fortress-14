using Content.Client._RF.CSS.Streams;
using Content.Client._RF.Graphics;
using JetBrains.Annotations;

namespace Content.Client._RF.CSS.Parsers;

/// <summary>
/// What a <see cref="BorderImageValue"/> was written as.
/// </summary>
public enum BorderImageValueKind : byte
{
    /// <summary>
    /// A unitless number.
    /// </summary>
    Number,

    /// <summary>
    /// A length or a percentage, see <see cref="BorderImageValue.Length"/>.
    /// </summary>
    Length,

    /// <summary>
    /// The <c>auto</c> keyword.
    /// </summary>
    Auto,
}

/// <summary>
/// One side of <c>border-image-width</c> or <c>border-image-outset</c> as it was written. The length is not
/// resolved yet: it may be in <c>em</c> and the like, which only the control can resolve.
/// </summary>
public readonly record struct BorderImageValue(BorderImageValueKind Kind, float Number, LengthUnit Length)
{
    public static BorderImageValue FromNumber(float number) => new(BorderImageValueKind.Number, number, LengthUnit.Zero);

    public static readonly BorderImageValue Auto = new(BorderImageValueKind.Auto, 0f, LengthUnit.Zero);
}

/// <summary>
/// <c>border-image-width</c>.
/// </summary>
public readonly record struct BorderImageWidth(
    BorderImageValue Top,
    BorderImageValue Right,
    BorderImageValue Bottom,
    BorderImageValue Left)
{
    /// <summary>
    /// The initial value: <c>1</c>, the width of the border.
    /// </summary>
    public static readonly BorderImageWidth Default = new(
        BorderImageValue.FromNumber(1f),
        BorderImageValue.FromNumber(1f),
        BorderImageValue.FromNumber(1f),
        BorderImageValue.FromNumber(1f));
}

/// <summary>
/// <c>border-image-outset</c>.
/// </summary>
public readonly record struct BorderImageOutset(
    BorderImageValue Top,
    BorderImageValue Right,
    BorderImageValue Bottom,
    BorderImageValue Left)
{
    /// <summary>
    /// The initial value: <c>0</c>.
    /// </summary>
    public static readonly BorderImageOutset Default = new(
        BorderImageValue.FromNumber(0f),
        BorderImageValue.FromNumber(0f),
        BorderImageValue.FromNumber(0f),
        BorderImageValue.FromNumber(0f));
}

/// <summary>
/// <c>border-image-slice</c>.
/// </summary>
public readonly record struct BorderImageSlice(BorderImageEdges Edges, bool Fill)
{
    /// <summary>
    /// The initial value: <c>100%</c>.
    /// </summary>
    public static readonly BorderImageSlice Default = new(new BorderImageEdges(BorderImageEdge.Percent(100f)), false);
}

/// <summary>
/// <c>border-image-repeat</c>.
/// </summary>
public readonly record struct BorderImageRepeat(BorderImageRepeatMode X, BorderImageRepeatMode Y)
{
    /// <summary>
    /// The initial value: <c>stretch</c>.
    /// </summary>
    public static readonly BorderImageRepeat Default = new(BorderImageRepeatMode.Stretch, BorderImageRepeatMode.Stretch);
}

/// <summary>
/// Parsing of the components of <c>border-image</c>. Each method consumes only its own part of the stream,
/// so the <c>border-image</c> shorthand can chain them.
/// </summary>
public static class BorderImageParsing
{
    /// <summary>
    /// <c>[ &lt;number&gt; | &lt;percentage&gt; | &lt;number&gt;px ]{1,4} &amp;&amp; fill?</c>
    /// </summary>
    public static bool TryParseSlice(ref CssValueStream stream, out BorderImageSlice value)
    {
        value = BorderImageSlice.Default;

        var edges = new List<BorderImageEdge>();
        var fill = false;

        while (!stream.AtEnd)
        {
            switch (stream.Peek())
            {
                case IdentToken { Value: "fill" } when !fill:
                    stream.ConsumeNext();
                    fill = true;
                    continue;
                case NumberToken { Value: >= 0 } number when edges.Count < 4:
                    stream.ConsumeNext();
                    edges.Add(BorderImageEdge.Number((float) number.Value));
                    continue;
                // Not in the specification, where a number already is in image pixels, but every other size of
                // this stylesheet is written with a unit, so `2px` is the same as `2`.
                case DimensionToken { Value: >= 0 } dimension
                    when edges.Count < 4 && dimension.Unit.Equals("px", StringComparison.OrdinalIgnoreCase):
                    stream.ConsumeNext();
                    edges.Add(BorderImageEdge.Number((float) dimension.Value));
                    continue;
                case PercentageToken { Value: >= 0 } percentage when edges.Count < 4:
                    stream.ConsumeNext();
                    edges.Add(BorderImageEdge.Percent((float) percentage.Value));
                    continue;
            }

            break;
        }

        if (edges.Count == 0)
            return false;

        value = new BorderImageSlice(Expand(edges), fill);
        return true;
    }

    /// <summary>
    /// <c>[ &lt;length-percentage [0,∞]&gt; | &lt;number [0,∞]&gt; | auto ]{1,4}</c>
    /// </summary>
    public static bool TryParseWidth(ref CssValueStream stream, ICssManager css, out BorderImageWidth value)
    {
        value = BorderImageWidth.Default;

        if (!TryParseValues(ref stream, css, allowAutoAndPercent: true, out var values))
            return false;

        value = new BorderImageWidth(values[0], values[1], values[2], values[3]);
        return true;
    }

    /// <summary>
    /// <c>[ &lt;length [0,∞]&gt; | &lt;number [0,∞]&gt; ]{1,4}</c>
    /// </summary>
    public static bool TryParseOutset(ref CssValueStream stream, ICssManager css, out BorderImageOutset value)
    {
        value = BorderImageOutset.Default;

        if (!TryParseValues(ref stream, css, allowAutoAndPercent: false, out var values))
            return false;

        value = new BorderImageOutset(values[0], values[1], values[2], values[3]);
        return true;
    }

    /// <summary>
    /// <c>[ stretch | repeat | round | space ]{1,2}</c>
    /// </summary>
    public static bool TryParseRepeat(ref CssValueStream stream, out BorderImageRepeat value)
    {
        value = BorderImageRepeat.Default;

        if (!TryParseRepeatMode(ref stream, out var x))
            return false;

        value = new BorderImageRepeat(x, TryParseRepeatMode(ref stream, out var y) ? y : x);
        return true;
    }

    private static bool TryParseRepeatMode(ref CssValueStream stream, out BorderImageRepeatMode mode)
    {
        mode = BorderImageRepeatMode.Stretch;

        switch (stream.Peek())
        {
            case IdentToken { Value: "stretch" }:
                break;
            case IdentToken { Value: "repeat" }:
                mode = BorderImageRepeatMode.Repeat;
                break;
            case IdentToken { Value: "round" }:
                mode = BorderImageRepeatMode.Round;
                break;
            case IdentToken { Value: "space" }:
                mode = BorderImageRepeatMode.Space;
                break;
            default:
                return false;
        }

        stream.ConsumeNext();
        return true;
    }

    private static bool TryParseValues(
        ref CssValueStream stream,
        ICssManager css,
        bool allowAutoAndPercent,
        out BorderImageValue[] values)
    {
        var list = new List<BorderImageValue>();

        while (list.Count < 4 && !stream.AtEnd)
        {
            if (!TryParseValue(ref stream, css, allowAutoAndPercent, out var v))
                break;

            list.Add(v);
        }

        values = list.Count == 0 ? [] : Expand(list);
        return list.Count != 0;
    }

    private static bool TryParseValue(ref CssValueStream stream, ICssManager css, bool allowAutoAndPercent, out BorderImageValue value)
    {
        value = default;

        switch (stream.Peek())
        {
            case IdentToken { Value: "auto" } when allowAutoAndPercent:
                stream.ConsumeNext();
                value = BorderImageValue.Auto;
                return true;
            case NumberToken { Value: >= 0 } number:
                stream.ConsumeNext();
                value = BorderImageValue.FromNumber((float) number.Value);
                return true;
            case NumberToken:
                return false;
            case PercentageToken when !allowAutoAndPercent:
                return false;
        }

        var mark = stream.Mark();

        if (!css.TryParseValue<LengthUnit>(ref stream, out var length) || length is { Type: not LengthUnitType.Calc, Value: < 0f })
        {
            stream.Reset(mark);
            return false;
        }

        value = new BorderImageValue(BorderImageValueKind.Length, 0f, length);
        return true;
    }

    private static T[] Expand<T>(List<T> values) => values.Count switch
    {
        1 => [values[0], values[0], values[0], values[0]],
        2 => [values[0], values[1], values[0], values[1]], // vertical horizontal
        3 => [values[0], values[1], values[2], values[1]], // top horizontal bottom
        _ => [values[0], values[1], values[2], values[3]], // top right bottom left
    };

    private static BorderImageEdges Expand(List<BorderImageEdge> edges)
    {
        var e = Expand<BorderImageEdge>(edges);
        return new BorderImageEdges(e[0], e[1], e[2], e[3]);
    }
}

[UsedImplicitly]
public sealed class BorderImageSliceParser : CssValueParser<BorderImageSlice>
{
    public override bool TryParse(ref CssValueStream stream, out BorderImageSlice value)
        => BorderImageParsing.TryParseSlice(ref stream, out value) && stream.AtEnd;
}

[UsedImplicitly]
public sealed class BorderImageWidthParser : CssValueParser<BorderImageWidth>
{
    public override bool TryParse(ref CssValueStream stream, out BorderImageWidth value)
        => BorderImageParsing.TryParseWidth(ref stream, Css, out value) && stream.AtEnd;
}

[UsedImplicitly]
public sealed class BorderImageOutsetParser : CssValueParser<BorderImageOutset>
{
    public override bool TryParse(ref CssValueStream stream, out BorderImageOutset value)
        => BorderImageParsing.TryParseOutset(ref stream, Css, out value) && stream.AtEnd;
}

[UsedImplicitly]
public sealed class BorderImageRepeatParser : CssValueParser<BorderImageRepeat>
{
    public override bool TryParse(ref CssValueStream stream, out BorderImageRepeat value)
        => BorderImageParsing.TryParseRepeat(ref stream, out value) && stream.AtEnd;
}
