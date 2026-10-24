using Content.Suil;
using Content.Client._RF.Graphics;
using Content.Shared._RF.Utilities;

namespace Content.Client._RF.Suil;

public partial class UiElement
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

    [SuilProperty]
    [SuilShorthand(BorderShorthandName, nameof(ParseBorderShorthand))]
    [SuilShorthand(BorderTopShorthandName, nameof(ParseBorderTopShorthand))]
    [SuilShorthand(BorderColorShorthandName, nameof(ParseBorderColorShorthand))]
    protected Color? BorderTopColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderRightShorthandName, nameof(ParseBorderRightShorthand))]
    [SuilShorthand(BorderColorShorthandName)]
    protected Color? BorderRightColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderBottomShorthandName, nameof(ParseBorderBottomShorthand))]
    [SuilShorthand(BorderColorShorthandName)]
    protected Color? BorderBottomColor
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderLeftShorthandName, nameof(ParseBorderLeftShorthand))]
    [SuilShorthand(BorderColorShorthandName)]
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

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderTopShorthandName)]
    [SuilShorthand(BorderWidthShorthandName, nameof(ParseBorderWidthShorthand))]
    protected LengthUnit BorderTopWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderRightShorthandName)]
    [SuilShorthand(BorderWidthShorthandName)]
    protected LengthUnit BorderRightWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderBottomShorthandName)]
    [SuilShorthand(BorderWidthShorthandName)]
    protected LengthUnit BorderBottomWidth
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    }

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderLeftShorthandName)]
    [SuilShorthand(BorderWidthShorthandName)]
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

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderTopShorthandName)]
    [SuilShorthand(BorderStyleShorthandName, nameof(ParseBorderStyleShorthand))]
    protected SuilBorderStyle BorderTopStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = SuilBorderStyle.None;

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderRightShorthandName)]
    [SuilShorthand(BorderStyleShorthandName)]
    protected SuilBorderStyle BorderRightStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = SuilBorderStyle.None;

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderBottomShorthandName)]
    [SuilShorthand(BorderStyleShorthandName)]
    protected SuilBorderStyle BorderBottomStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = SuilBorderStyle.None;

    [SuilProperty]
    [SuilShorthand(BorderShorthandName)]
    [SuilShorthand(BorderLeftShorthandName)]
    [SuilShorthand(BorderStyleShorthandName)]
    protected SuilBorderStyle BorderLeftStyle
    {
        get;
        set
        {
            field = value;
            Background?.Border = BuildBorder();
        }
    } = SuilBorderStyle.None;

    // ── Per-corner radius ────────────────────────────────────────────

    [SuilProperty]
    [SuilShorthand(BorderRadiusShorthandName, nameof(ParseBorderRadiusShorthand))]
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

    [SuilProperty]
    [SuilShorthand(BorderRadiusShorthandName)]
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

    [SuilProperty]
    [SuilShorthand(BorderRadiusShorthandName)]
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

    [SuilProperty]
    [SuilShorthand(BorderRadiusShorthandName)]
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
            BorderTopStyle is SuilBorderStyle.None or SuilBorderStyle.Hidden ? 0f : ToPx(BorderTopWidth));
        var right = new BorderSide(
            BorderRightStyle,
            BorderRightColor ?? Color.Black,
            BorderRightStyle is SuilBorderStyle.None or SuilBorderStyle.Hidden ? 0f : ToPx(BorderRightWidth));
        var bottom = new BorderSide(
            BorderBottomStyle,
            BorderBottomColor ?? Color.Black,
            BorderBottomStyle is SuilBorderStyle.None or SuilBorderStyle.Hidden ? 0f : ToPx(BorderBottomWidth));
        var left = new BorderSide(
            BorderLeftStyle,
            BorderLeftColor ?? Color.Black,
            BorderLeftStyle is SuilBorderStyle.None or SuilBorderStyle.Hidden ? 0f : ToPx(BorderLeftWidth));

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
    private static bool ParseBorderShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, parsers, values, "top", "right", "bottom", "left");

    /// <summary>
    /// "border-top"/"-right"/"-bottom"/"-left" — sets ONE side's color/width/style.
    /// </summary>
    private static bool ParseBorderTopShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, parsers, values, "top");

    private static bool ParseBorderRightShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, parsers, values, "right");

    private static bool ParseBorderBottomShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, parsers, values, "bottom");

    private static bool ParseBorderLeftShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
        => ParseBorderTriple(ref stream, parsers, values, "left");

    private static bool ParseBorderTriple(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values, params string[] sides)
    {
        Color? color = null;
        LengthUnit? width = null;
        SuilBorderStyle? style = null;

        while (!stream.AtEnd)
        {
            if (stream.Peek() is SuilIdent { Value: var s }
                && Enum.TryParse<SuilBorderStyle>(RfCaseConversion.KebabToPascal(s), ignoreCase: true, out var parsedStyle))
            {
                stream.ConsumeNext();
                style = parsedStyle;
                continue;
            }

            var mark = stream.Mark();
            if (parsers.TryParse<LengthUnit>(ref stream, out var w))
            {
                width = w;
                continue;
            }
            stream.Reset(mark);

            if (parsers.TryParse<Color>(ref stream, out var c))
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
            values[$"border-{side}-style"] = style ?? SuilBorderStyle.None;
        }

        return true;
    }

    /// <summary>
    /// "border-color" — 1/2/4-value SUIL clockwise expansion
    /// (top, right, bottom, left), same packing rule as "margin"/"padding".
    /// </summary>
    private static bool ParseBorderColorShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        var colors = new List<Color>();
        while (colors.Count < 4 && !stream.AtEnd)
        {
            if (!parsers.TryParse<Color>(ref stream, out var c))
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
    private static bool ParseBorderWidthShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        var widths = new List<LengthUnit>();
        while (widths.Count < 4 && !stream.AtEnd)
        {
            if (!parsers.TryParse<LengthUnit>(ref stream, out var w))
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

    private static bool ParseBorderStyleShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        var styles = new List<SuilBorderStyle>();
        while (styles.Count < 4 && !stream.AtEnd)
        {
            if (stream.Peek() is not SuilIdent { Value: var s }
                || !Enum.TryParse<SuilBorderStyle>(RfCaseConversion.KebabToPascal(s), ignoreCase: true, out var parsed))
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

    private static bool ParseBorderRadiusShorthand(ref SuilValueStream stream, ISuilValues parsers, Dictionary<string, object?> values)
    {
        if (!TryParseLengthPercentageGroup(ref stream, parsers, out var horizontal))
            return false;

        var vertical = horizontal;
        if (stream.Peek() is SuilDelim { CodePoint: '/' })
        {
            stream.ConsumeNext();
            if (!TryParseLengthPercentageGroup(ref stream, parsers, out vertical))
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

    private static bool TryParseLengthPercentageGroup(ref SuilValueStream stream, ISuilValues parsers, out LengthUnit[] group)
    {
        var values = new List<LengthUnit>();

        while (values.Count < 4 && stream.Peek() is not (null or SuilDelim { CodePoint: '/' }))
        {
            if (stream.Peek() is SuilPercentage percent)
            {
                values.Add(new LengthUnit((float)percent.Value, LengthUnitType.Percent));
                stream.ConsumeNext();
                continue;
            }

            if (!parsers.TryParse<LengthUnit>(ref stream, out var length))
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
/// Which SUIL <c>border-style</c> keyword a border segment should render as.
/// Determines which concrete <see cref="StyleBoxBorderStroke"/> subtype a SUIL
/// <c>border-style</c> declaration is mapped to.
/// </summary>
public enum SuilBorderStyle : byte
{
    /// <summary>
    /// No border is drawn (equivalent to omitting the border entirely).
    /// </summary>
    None,

    /// <summary>
    /// Same rendering as <see cref="None"/>, but distinct for SUIL border-collapse
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
