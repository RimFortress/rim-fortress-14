using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.CSS.Streams;
using Content.Client._RF.Graphics;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    private const string BackgroundShorthandName = "background";
    private const string BackgroundPositionShorthandName = "background-position";
    private const string PaddingShorthandName = "padding";
    private const string ExpandPatchMarginShorthandName = "expand-patch-margin";
    private const string PatchMarginShorthandName = "patch-margin";

    /// <summary>
    /// Current control background.
    /// </summary>
    protected RfStyleBox? Background
    {
        get;
        private set
        {
            field = value;

            // Padding and borders are part of the content inset, so the layout depends on the background.
            InvalidateMeasure();
        }
    }

    /// <summary>
    /// Sets the background color of an element. The background of an element is the
    /// total size of the element, including padding and border (but not the margin).
    /// </summary>
    /// <remarks>
    /// If a texture is used as the background, the color will be used to modulate the texture.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName, nameof(ParseBackgroundShorthand))]
    public Color? BackgroundColor // public for test
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the texture of the control's background image.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    protected Texture? BackgroundImage
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets if/how a background image will be repeated.
    /// By default, a <see cref="BackgroundImage"/> is repeated
    /// both vertically and horizontally.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    protected RfStyleBoxTexture.StretchMode BackgroundRepeat
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = RfStyleBoxTexture.StretchMode.Repeat;

    /// <summary>
    /// Specifies the size of the background images.
    /// There are four different syntaxes you can use with this property:
    /// the keyword syntax ("auto", "cover" and "contain"), the one-value syntax
    /// (sets the width of the image (height becomes "auto"), the two-value syntax
    /// (first value: width of the image, second value: height),
    /// and the multiple background syntax (separated with comma).
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    protected RfStyleBoxTexture.StyleBoxSize BackgroundSize
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = RfStyleBoxTexture.StyleBoxSize.Auto;

    /// <summary>
    /// Horizontal component of the background image's starting position.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    [CssShorthandProperty(BackgroundPositionShorthandName, nameof(ParseBackgroundPositionShorthand))]
    protected StyleBoxPositionComponent BackgroundPositionX
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = StyleBoxPositionComponent.Zero;

    /// <summary>
    /// Vertical component of the background image's starting position.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    [CssShorthandProperty(BackgroundPositionShorthandName)]
    protected StyleBoxPositionComponent BackgroundPositionY
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = StyleBoxPositionComponent.Zero;

    /// <summary>
    /// Combines <see cref="BackgroundPositionX"/> and <see cref="BackgroundPositionY"/>.
    /// Setting this assigns both components at once (mirrors the CSS background-position
    /// shorthand); the longhand background-position-x/-y properties remain independently settable.
    /// </summary>
    protected RfStyleBoxTexture.StyleBoxPosition BackgroundPosition
    {
        get => new()
        {
            X = BackgroundPositionX.Value, XIsAbsolute = BackgroundPositionX.IsAbsolute, XCalc = BackgroundPositionX.Calc,
            Y = BackgroundPositionY.Value, YIsAbsolute = BackgroundPositionY.IsAbsolute, YCalc = BackgroundPositionY.Calc,
        };
        set
        {
            BackgroundPositionX = new StyleBoxPositionComponent(value.X, value.XIsAbsolute, value.XCalc);
            BackgroundPositionY = new StyleBoxPositionComponent(value.Y, value.YIsAbsolute, value.YCalc);
        }
    }

    /// <summary>
    /// Defines how far the background (color, image, or gradient) should extend within an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    protected StyleBoxBackgroundClip BackgroundClip
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = StyleBoxBackgroundClip.BorderBox;

    /// <summary>
    /// Sets the reference box for resolving background-position/background-size
    /// percentages and the tiling pattern's phase. See <see cref="StyleBoxBackgroundOrigin"/>.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    protected StyleBoxBackgroundOrigin BackgroundOrigin
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    } = StyleBoxBackgroundOrigin.PaddingBox;

    /// <summary>
    /// Sets the width of the padding area to the left of an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PaddingShorthandName, nameof(ParsePaddingShorthand))]
    protected LengthUnit PaddingLeft
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the height of the padding area on the top of an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PaddingShorthandName)]
    protected LengthUnit PaddingTop
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the width of the padding area to the right of an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PaddingShorthandName)]
    protected LengthUnit PaddingRight
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the height of the padding area on the bottom of an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PaddingShorthandName)]
    protected LengthUnit PaddingBottom
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the padding area on all four sides of an element at once.
    /// </summary>
    protected Thickness Padding
    {
        get => ToPaddingThickness();
        set
        {
            PaddingLeft = value.Left;
            PaddingTop = value.Top;
            PaddingRight = value.Right;
            PaddingBottom = value.Bottom;
        }
    }

    /// <summary>
    /// Left expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(ExpandPatchMarginShorthandName, nameof(ParseExpandMarginShorthand))]
    protected float ExpandPatchMarginLeft
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Top expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(ExpandPatchMarginShorthandName)]
    protected float ExpandPatchMarginTop
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Bottom expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(ExpandPatchMarginShorthandName)]
    protected float ExpandPatchMarginBottom
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Right expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(ExpandPatchMarginShorthandName)]
    protected float ExpandPatchMarginRight
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Distance of the left patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling
    /// this with <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PatchMarginShorthandName, nameof(ParsePatchMarginShorthand))]
    protected float PatchMarginLeft
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Distance of the right patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling
    /// this with <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PatchMarginShorthandName)]
    protected float PatchMarginRight
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Distance of the top patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling
    /// this with <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PatchMarginShorthandName)]
    protected float PatchMarginTop
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Distance of the bottom patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling
    /// this with <see cref="RfStyleBoxTexture.TextureScale"/>.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(PatchMarginShorthandName)]
    protected float PatchMarginBottom
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    // A style box knows its area but not the element, so what depends on the element is resolved here.
    private RfStyleBoxTexture.StyleBoxSize BindBackgroundSize(RfStyleBoxTexture.StyleBoxSize size)
    {
        size.WidthCalc = size.WidthCalc?.Bind(LengthContext);
        size.HeightCalc = size.HeightCalc?.Bind(LengthContext);
        return size;
    }

    private RfStyleBoxTexture.StyleBoxPosition BindBackgroundPosition(RfStyleBoxTexture.StyleBoxPosition position)
    {
        position.XCalc = position.XCalc?.Bind(LengthContext);
        position.YCalc = position.YCalc?.Bind(LengthContext);
        return position;
    }

    private RfStyleBox GetBackground()
    {
        RfStyleBox box = BackgroundImage != null
            ? new RfStyleBoxTexture
            {
                Texture = BackgroundImage,
                Modulate = BackgroundColor ?? Color.White,
                Mode = BackgroundRepeat,
                Size = BindBackgroundSize(BackgroundSize),
                Position = BindBackgroundPosition(BackgroundPosition),
                ExpandMarginBottom = ExpandPatchMarginBottom,
                ExpandMarginLeft = ExpandPatchMarginLeft,
                ExpandMarginRight = ExpandPatchMarginRight,
                ExpandMarginTop = ExpandPatchMarginTop,
                PatchMarginBottom = PatchMarginBottom,
                PatchMarginLeft = PatchMarginLeft,
                PatchMarginRight = PatchMarginRight,
                PatchMarginTop = PatchMarginTop,
            }
            : new RfStyleBoxFlat { BackgroundColor = BackgroundColor ?? Color.Transparent };

        box.BackgroundClip = BackgroundClip;
        box.BackgroundOrigin = BackgroundOrigin;
        box.Padding = Padding;
        box.Border = BuildBorder();
        box.Radius = ResolveBorderRadius(BorderRadius);

        return box;
    }

    /// <summary>
    /// Draws the control <see cref="Background"/>.
    /// </summary>
    protected virtual void DrawBackground(DrawingHandleScreen handle)
    {
        if (Background is { } box)
            box.Draw(handle, PixelSizeBox, UIScale);
    }

    #region Parsing

    /// <summary>
    /// Custom parser for the "background" shorthand (<see cref="CssShorthandParserDelegate"/>).
    /// A generic per-token greedy match can't handle this one: background-color
    /// and background-image share overlapping token shapes with other
    /// keyword-based components, and background-position/background-size are
    /// linked by an optional "/" separator rather than being independently
    /// recognizable. Every longhand this shorthand covers is written into
    /// <paramref name="values"/> with its CSS-spec initial value up front —
    /// not left to CssManager's blind default(ClrType) fallback — because
    /// that fallback wouldn't reliably match this class's own declared
    /// defaults (e.g. BackgroundRepeat's default is Repeat, not necessarily
    /// the zero value of its enum).
    /// </summary>
    private static bool ParseBackgroundShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        Color? color = null;
        Texture? image = null;
        var repeat = RfStyleBoxTexture.StretchMode.Repeat;
        var size = RfStyleBoxTexture.StyleBoxSize.Auto;
        var positionX = StyleBoxPositionComponent.Zero;
        var positionY = StyleBoxPositionComponent.Zero;
        var sawPosition = false;
        var clip = StyleBoxBackgroundClip.BorderBox;
        var origin = StyleBoxBackgroundOrigin.BorderBox;

        while (!stream.AtEnd)
        {
            if (stream.Peek() is UrlToken)
            {
                if (!css.TryParseValue<Texture>(ref stream, out var v))
                    return false;

                image = v;
                continue;
            }

            if (stream.Peek() is IdentToken { Value: "none" })
            {
                stream.ConsumeNext();
                continue; // image stays null
            }

            if (stream.Peek() is IdentToken { Value: "repeat" or "repeat-x" or "repeat-y" or "no-repeat" })
            {
                if (!css.TryParseValue<RfStyleBoxTexture.StretchMode>(ref stream, out var v))
                    return false;

                repeat = v;
                continue;
            }

            if (stream.Peek() is IdentToken { Value: "border-box" or "padding-box" or "content-box" })
            {
                if (!css.TryParseValue<StyleBoxBackgroundOrigin>(ref stream, out var o))
                    return false;

                origin = o;

                if (stream.Peek() is IdentToken { Value: "border-box" or "padding-box" or "content-box" }
                    && css.TryParseValue<StyleBoxBackgroundClip>(ref stream, out var cl))
                    clip = cl;

                continue;
            }

            if (!sawPosition && LooksLikePositionStart(stream))
            {
                if (!TryParsePositionComponents(ref stream, out var x, out var y))
                    return false;

                positionX = x;
                positionY = y;
                sawPosition = true;

                if (stream.Peek() is DelimToken { CodePoint: '/' })
                {
                    stream.ConsumeNext();

                    if (!css.TryParseValue<RfStyleBoxTexture.StyleBoxSize>(ref stream, out var s))
                        return false;

                    size = s;
                }
                continue;
            }

            if (!css.TryParseValue<Color>(ref stream, out var c))
                return false;

            color = c;
        }

        values["background-color"] = color ?? Color.Transparent;
        values["background-image"] = image;
        values["background-repeat"] = repeat;
        values["background-size"] = size;
        values["background-origin"] = origin;
        values["background-clip"] = clip;
        values["background-position-x"] = positionX;
        values["background-position-y"] = positionY;
        return true;
    }

    /// <summary>
    /// Custom parser for the "background-position" shorthand
    /// (<see cref="CssShorthandParserDelegate"/>). Needed rather than the
    /// generic greedy expander because background-position-x and
    /// background-position-y share the exact same CLR type
    /// (<see cref="StyleBoxPositionComponent"/>) — a type-based greedy match
    /// can't tell which token belongs to which axis, and the single-value
    /// form's "missing axis defaults to center" rule isn't a per-type default
    /// at all (the property's own reset default is 0%, not center — center
    /// only applies to the axis a single-value declaration leaves unstated).
    /// </summary>
    private static bool ParseBackgroundPositionShorthand(ref CssValueStream stream, ICssManager css, Dictionary<string, object?> values)
    {
        if (!TryParsePositionComponents(ref stream, out var x, out var y))
            return false;

        if (!stream.AtEnd)
            return false;

        values["background-position-x"] = x;
        values["background-position-y"] = y;
        return true;
    }

    private static bool TryParsePositionComponents(ref CssValueStream stream, out StyleBoxPositionComponent x, out StyleBoxPositionComponent y)
    {
        x = StyleBoxPositionComponent.Zero;
        y = StyleBoxPositionComponent.Zero;

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var xValue, out var xAbs, out var xCalc, isXAxis: true))
            return false;

        x = new StyleBoxPositionComponent(xValue, xAbs, xCalc);

        if (stream.AtEnd || LooksLikePositionEnd(stream))
        {
            y = new StyleBoxPositionComponent(0.5f, false); // single value: Y defaults to center, per spec
            return true;
        }

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var yValue, out var yAbs, out var yCalc, isXAxis: false))
            return false;

        y = new StyleBoxPositionComponent(yValue, yAbs, yCalc);
        return true;
    }

    private static bool LooksLikePositionStart(CssValueStream stream) => stream.Peek() is
        IdentToken { Value: "left" or "right" or "top" or "bottom" or "center" }
        or PercentageToken
        or DimensionToken
        or CssFunction
        or NumberToken { Value: 0 };

    // Used inside the "background" shorthand, where position parsing must
    // stop at "/" (the size separator) or a non-position token (color etc.),
    // rather than requiring the whole stream to end.
    private static bool LooksLikePositionEnd(CssValueStream stream) =>
        stream.Peek() is DelimToken { CodePoint: '/' } || !LooksLikePositionStart(stream);

    private static bool ParsePaddingShorthand(
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
        => ParseMarginLikeShorthand<LengthUnit>(
            "padding-top",
            "padding-right",
            "padding-bottom",
            "padding-left",
            ref stream,
            css,
            values);

    private static bool ParseExpandMarginShorthand(
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
        => ParseMarginLikeShorthand<float>(
            "expand-margin-top",
            "expand-margin-right",
            "expand-margin-bottom",
            "expand-margin-left",
            ref stream,
            css,
            values);

    private static bool ParsePatchMarginShorthand(
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
        => ParseMarginLikeShorthand<float>(
            "patch-margin-top",
            "patch-margin-right",
            "patch-margin-bottom",
            "patch-margin-left",
            ref stream,
            css,
            values);

    private static bool ParseMarginLikeShorthand<T>(
        string property1,
        string property2,
        string property3,
        string property4,
        ref CssValueStream stream,
        ICssManager css,
        Dictionary<string, object?> values)
    {
        var lengths = new List<T>();

        while (lengths.Count < 4 && !stream.AtEnd)
        {
            if (!css.TryParseValue<T>(ref stream, out var v))
                break;

            lengths.Add(v!);
        }

        if (!stream.AtEnd || lengths.Count is < 1 or > 4)
            return false;

        var (top, right, bottom, left) = lengths.Count switch
        {
            1 => (lengths[0], lengths[0], lengths[0], lengths[0]),
            2 => (lengths[0], lengths[1], lengths[0], lengths[1]), // CSS: vertical horizontal
            3 => (lengths[0], lengths[1], lengths[2], lengths[1]), // CSS: top horizontal bottom
            4 => (lengths[0], lengths[1], lengths[2], lengths[3]), // CSS: top right bottom left
            _ => default,
        };

        values[property1] = top;
        values[property2] = right;
        values[property3] = bottom;
        values[property4] = left;
        return true;
    }

    #endregion
}

/// <summary>
/// One axis of a background-position value.
/// </summary>
public record struct StyleBoxPositionComponent(float Value, bool IsAbsolute, CalcNode? Calc = null)
{
    public static readonly StyleBoxPositionComponent Zero = new() { Value = 0f };
}
