using Content.Client._RF.CSS.Streams;
using Content.Client._RF.Graphics;
using Content.Shared._RF.Utilities;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    private const string BorderShorthandName = "border";
    private const string BorderTopShorthandName = "border-top";
    private const string BorderRightShorthandName = "border-right";
    private const string BorderBottomShorthandName = "border-bottom";
    private const string BorderLeftShorthandName = "border-left";
    private const string BorderColorShorthandName = "border-color";
    private const string BorderWidthShorthandName = "border-width";
    private const string BorderStyleShorthandName = "border-style";
    private const string BorderRadiusShorthandName = "border-radius";

    // ── Per-side color ──────────────────────────────────────────────

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName, nameof(ParseBorderShorthand))]
    [CssShorthandProperty(BorderTopShorthandName, nameof(ParseBorderTopShorthand))]
    [CssShorthandProperty(BorderColorShorthandName, nameof(ParseBorderColorShorthand))]
    protected Color? BorderTopColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderRightShorthandName, nameof(ParseBorderRightShorthand))]
    [CssShorthandProperty(BorderColorShorthandName)]
    protected Color? BorderRightColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderBottomShorthandName, nameof(ParseBorderBottomShorthand))]
    [CssShorthandProperty(BorderColorShorthandName)]
    protected Color? BorderBottomColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderLeftShorthandName, nameof(ParseBorderLeftShorthand))]
    [CssShorthandProperty(BorderColorShorthandName)]
    protected Color? BorderLeftColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    // ── Per-side width ──────────────────────────────────────────────

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderTopShorthandName)]
    [CssShorthandProperty(BorderWidthShorthandName, nameof(ParseBorderWidthShorthand))]
    protected LengthUnit BorderTopWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderRightShorthandName)]
    [CssShorthandProperty(BorderWidthShorthandName)]
    protected LengthUnit BorderRightWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderBottomShorthandName)]
    [CssShorthandProperty(BorderWidthShorthandName)]
    protected LengthUnit BorderBottomWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderLeftShorthandName)]
    [CssShorthandProperty(BorderWidthShorthandName)]
    protected LengthUnit BorderLeftWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    // ── Per-side style ───────────────────────────────────────────────

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderTopShorthandName)]
    [CssShorthandProperty(BorderStyleShorthandName, nameof(ParseBorderStyleShorthand))]
    protected CssBorderStyle BorderTopStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = CssBorderStyle.None;

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderRightShorthandName)]
    [CssShorthandProperty(BorderStyleShorthandName)]
    protected CssBorderStyle BorderRightStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = CssBorderStyle.None;

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderBottomShorthandName)]
    [CssShorthandProperty(BorderStyleShorthandName)]
    protected CssBorderStyle BorderBottomStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = CssBorderStyle.None;

    [CssProperty]
    [CssShorthandProperty(BorderShorthandName)]
    [CssShorthandProperty(BorderLeftShorthandName)]
    [CssShorthandProperty(BorderStyleShorthandName)]
    protected CssBorderStyle BorderLeftStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = CssBorderStyle.None;

    // ── Per-corner radius ────────────────────────────────────────────

    [CssProperty]
    [CssShorthandProperty(BorderRadiusShorthandName, nameof(ParseBorderRadiusShorthand))]
    protected UnitVector2 BorderTopLeftRadius
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
            Background?.Radius = ResolveBorderRadius(BorderRadius);
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderRadiusShorthandName)]
    protected UnitVector2 BorderTopRightRadius
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
            Background?.Radius = ResolveBorderRadius(BorderRadius);
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderRadiusShorthandName)]
    protected UnitVector2 BorderBottomRightRadius
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
            Background?.Radius = ResolveBorderRadius(BorderRadius);
        }
    }

    [CssProperty]
    [CssShorthandProperty(BorderRadiusShorthandName)]
    protected UnitVector2 BorderBottomLeftRadius
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
            Background?.Radius = ResolveBorderRadius(BorderRadius);
        }
    }

    protected CornerRadius BorderRadius
    {
        get => new(BorderTopLeftRadius, BorderTopRightRadius, BorderBottomRightRadius, BorderBottomLeftRadius);
        set
        {
            BorderTopLeftRadius = value.TopLeft;
            BorderTopRightRadius = value.TopRight;
            BorderBottomRightRadius = value.BottomRight;
            BorderBottomLeftRadius = value.BottomLeft;
            Background?.Border = BuildBorder();
        }
    }

    // Font-relative and viewport units can't be resolved by the style box (it only knows its own size),
    // so everything except percentages (and calculations that contain them) is converted to pixels here.
    private CornerRadius ResolveBorderRadius(CornerRadius radius) => new(
        ResolveRadius(radius.TopLeft),
        ResolveRadius(radius.TopRight),
        ResolveRadius(radius.BottomRight),
        ResolveRadius(radius.BottomLeft));

    private UnitVector2 ResolveRadius(UnitVector2 radius) => new(ResolveRadiusComponent(radius.X), ResolveRadiusComponent(radius.Y));

    private LengthUnit ResolveRadiusComponent(LengthUnit length)
        => length switch
        {
            { IsPercent: true } => length,
            { Calc: { HasPercent: true } calc } => length with { Calc = calc.Bind(LengthContext) }, // finished by the style box
            _ => LengthUnit.Px(ToPx(length)),
        };

    private StyleBoxBorder? BuildBorder()
    {
        var top = new BorderSide(
            BorderTopStyle,
            BorderTopColor ?? Color.Black,
            BorderTopStyle is CssBorderStyle.None or CssBorderStyle.Hidden ? 0f : ToPx(BorderTopWidth));
        var right = new BorderSide(
            BorderRightStyle,
            BorderRightColor ?? Color.Black,
            BorderRightStyle is CssBorderStyle.None or CssBorderStyle.Hidden ? 0f : ToPx(BorderRightWidth));
        var bottom = new BorderSide(
            BorderBottomStyle,
            BorderBottomColor ?? Color.Black,
            BorderBottomStyle is CssBorderStyle.None or CssBorderStyle.Hidden ? 0f : ToPx(BorderBottomWidth));
        var left = new BorderSide(
            BorderLeftStyle,
            BorderLeftColor ?? Color.Black,
            BorderLeftStyle is CssBorderStyle.None or CssBorderStyle.Hidden ? 0f : ToPx(BorderLeftWidth));

        if (top.Width <= 0 && right.Width <= 0 && bottom.Width <= 0 && left.Width <= 0)
            return null;

        return (StyleBoxBorder?) BuildBorderImage(top, right, bottom, left)
               ?? new StyleBoxBorderStroke(ResolveBorderRadius(BorderRadius), top, right, bottom, left);
    }

    #region Parsing

    /// <summary>
    /// "border" shorthand — sets color/width/style uniformly on all four
    /// sides at once. Order-independent, any component may be omitted.
    /// </summary>
    private static bool ParseBorderShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, css, values, "top", "right", "bottom", "left");

    /// <summary>
    /// "border-top"/"-right"/"-bottom"/"-left" — sets ONE side's color/width/style.
    /// </summary>
    private static bool ParseBorderTopShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, css, values, "top");

    private static bool ParseBorderRightShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, css, values, "right");

    private static bool ParseBorderBottomShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, css, values, "bottom");

    private static bool ParseBorderLeftShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, css, values, "left");

    private static bool ParseBorderTriple(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values, params string[] sides)
    {
        Color? color = null;
        LengthUnit? width = null;
        CssBorderStyle? style = null;

        while (!stream.AtEnd)
        {
            if (stream.Peek() is IdentToken { Value: var s }
                && Enum.TryParse<CssBorderStyle>(RfCaseConversion.KebabToPascal(s), ignoreCase: true, out var parsedStyle))
            {
                stream.ConsumeNext();
                style = parsedStyle;
                continue;
            }

            var mark = stream.Mark();
            if (css.TryParseValue<LengthUnit>(ref stream, out var w))
            {
                width = w;
                continue;
            }
            stream.Reset(mark);

            if (css.TryParseValue<Color>(ref stream, out var c))
            {
                color = c;
                continue;
            }

            return false;
        }

        foreach (var side in sides)
        {
            values[$"border-{side}-color"] = color;
            values[$"border-{side}-width"] = width ?? LengthUnit.Zero;
            values[$"border-{side}-style"] = style ?? CssBorderStyle.None;
        }

        return true;
    }

    /// <summary>
    /// "border-color" — 1/2/4-value CSS clockwise expansion
    /// (top, right, bottom, left), same packing rule as "margin"/"padding".
    /// </summary>
    private static bool ParseBorderColorShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        var colors = new List<Color>();
        while (colors.Count < 4 && !stream.AtEnd)
        {
            if (!css.TryParseValue<Color>(ref stream, out var c))
                break;

            colors.Add(c);
        }

        if (!stream.AtEnd || colors.Count is not (1 or 2 or 4))
            return false;

        var (top, right, bottom, left) = colors.Count switch
        {
            1 => (colors[0], colors[0], colors[0], colors[0]),
            2 => (colors[0], colors[1], colors[0], colors[1]),
            4 => (colors[0], colors[1], colors[2], colors[3]),
            _ => default,
        };

        values["border-top-color"] = top;
        values["border-right-color"] = right;
        values["border-bottom-color"] = bottom;
        values["border-left-color"] = left;
        return true;
    }

    /// <summary>
    /// "border-width" — same 1/2/4-value clockwise expansion as "border-color".
    /// </summary>
    private static bool ParseBorderWidthShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        var widths = new List<LengthUnit>();
        while (widths.Count < 4 && !stream.AtEnd)
        {
            if (!css.TryParseValue<LengthUnit>(ref stream, out var w))
                break;

            widths.Add(w);
        }

        if (!stream.AtEnd || widths.Count is not (1 or 2 or 4))
            return false;

        var (top, right, bottom, left) = widths.Count switch
        {
            1 => (widths[0], widths[0], widths[0], widths[0]),
            2 => (widths[0], widths[1], widths[0], widths[1]),
            4 => (widths[0], widths[1], widths[2], widths[3]),
            _ => default,
        };

        values["border-top-width"] = top;
        values["border-right-width"] = right;
        values["border-bottom-width"] = bottom;
        values["border-left-width"] = left;
        return true;
    }

    private static bool ParseBorderStyleShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        var styles = new List<CssBorderStyle>();
        while (styles.Count < 4 && !stream.AtEnd)
        {
            if (stream.Peek() is not IdentToken { Value: var s }
                || !Enum.TryParse<CssBorderStyle>(RfCaseConversion.KebabToPascal(s), ignoreCase: true, out var parsed))
                break;
            stream.ConsumeNext();
            styles.Add(parsed);
        }

        if (!stream.AtEnd || styles.Count is not (1 or 2 or 3 or 4))
            return false;

        var (top, right, bottom, left) = styles.Count switch
        {
            1 => (styles[0], styles[0], styles[0], styles[0]),
            2 => (styles[0], styles[1], styles[0], styles[1]),
            3 => (styles[0], styles[1], styles[2], styles[1]),
            4 => (styles[0], styles[1], styles[2], styles[3]),
            _ => default,
        };

        values["border-top-style"] = top;
        values["border-right-style"] = right;
        values["border-bottom-style"] = bottom;
        values["border-left-style"] = left;
        return true;
    }

    private static bool ParseBorderRadiusShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        if (!TryParseLengthPercentageGroup(ref stream, css, out var horizontal))
            return false;

        var vertical = horizontal;
        if (stream.Peek() is DelimToken { CodePoint: '/' })
        {
            stream.ConsumeNext();
            if (!TryParseLengthPercentageGroup(ref stream, css, out vertical))
                return false;
        }

        if (!stream.AtEnd)
            return false;

        values["border-top-left-radius"] = new UnitVector2(horizontal[0], vertical[0]);
        values["border-top-right-radius"] = new UnitVector2(horizontal[1], vertical[1]);
        values["border-bottom-right-radius"] = new UnitVector2(horizontal[2], vertical[2]);
        values["border-bottom-left-radius"] = new UnitVector2(horizontal[3], vertical[3]);
        return true;
    }

    private static bool TryParseLengthPercentageGroup(ref CssValueStream stream, ICssManager css, out LengthUnit[] group)
    {
        var values = new List<LengthUnit>();

        while (values.Count < 4 && stream.Peek() is not (null or DelimToken { CodePoint: '/' }))
        {
            if (stream.Peek() is PercentageToken percent)
            {
                values.Add(new LengthUnit((float)percent.Value, LengthUnitType.Percent));
                stream.ConsumeNext();
                continue;
            }

            if (!css.TryParseValue<LengthUnit>(ref stream, out var length))
                break;

            values.Add(length);
        }

        group = values.Count switch
        {
            1 => [values[0], values[0], values[0], values[0]],
            2 => [values[0], values[1], values[0], values[1]],
            3 => [values[0], values[1], values[2], values[1]],
            4 => [values[0], values[1], values[2], values[3]],
            _ => [],
        };

        return group.Length != 0;
    }

    #endregion
}

/// <summary>
/// Which CSS <c>border-style</c> keyword a border segment should render as.
/// Determines which concrete <see cref="StyleBoxBorderStroke"/> subtype a CSS
/// <c>border-style</c> declaration is mapped to.
/// </summary>
public enum CssBorderStyle : byte
{
    /// <summary>
    /// No border is drawn (equivalent to omitting the border entirely).
    /// </summary>
    None,

    /// <summary>
    /// Same rendering as <see cref="None"/>, but distinct for CSS border-collapse
    /// semantics on table-like layouts (not otherwise meaningful here).
    /// </summary>
    Hidden,

    /// <summary>
    /// A single solid-colored stroke.
    /// </summary>
    Solid,

    /// <summary>
    /// A stroke of alternating filled segments and gaps, longer than <see cref="Dotted"/>.
    /// </summary>
    Dashed,

    /// <summary>
    /// A stroke of alternating filled segments and gaps, roughly square segments.
    /// </summary>
    Dotted,

    /// <summary>
    /// Two parallel solid stripes with a gap between them.
    /// </summary>
    Double,

    /// <summary>
    /// A carved-in 3D look — outer half darker, inner half lighter.
    /// </summary>
    Groove,

    /// <summary>
    /// A raised-out 3D look — outer half lighter, inner half darker.
    /// </summary>
    Ridge,

    /// <summary>
    /// The whole border appears pressed in — every side uses the darker shade on its outer edge.
    /// </summary>
    Inset,

    /// <summary>
    /// The whole border appears popped out — every side uses the lighter shade on its outer edge.
    /// </summary>
    Outset,
}
