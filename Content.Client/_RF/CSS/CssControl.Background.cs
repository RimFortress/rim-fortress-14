using Content.Client._RF.CSS.Parsers;
using Content.Client._RF.Graphics;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    private const string BackgroundShorthandName = "background";
    private const string BackgroundPositionShorthandName = "background-position";

    /// <summary>
    /// Current control background.
    /// </summary>
    public StyleBox? Background { get; private set; }

    /// <summary>
    /// Sets the background color of an element. The background of an element is the
    /// total size of the element, including padding and border (but not the margin).
    /// </summary>
    /// <remarks>
    /// If a texture is used as the background, the color will be used to modulate the texture.
    /// </remarks>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName, nameof(ParseBackgroundShorthand))]
    public Color? BackgroundColor
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
    public Texture? BackgroundImage
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
    public RfStyleBoxTexture.StretchMode BackgroundRepeat
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
    public RfStyleBoxTexture.StyleBoxSize BackgroundSize
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
    public StyleBoxPositionComponent BackgroundPositionX
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
    public StyleBoxPositionComponent BackgroundPositionY
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
    public RfStyleBoxTexture.StyleBoxPosition BackgroundPosition
    {
        get => new()
        {
            X = BackgroundPositionX.Value, XIsAbsolute = BackgroundPositionX.IsAbsolute,
            Y = BackgroundPositionY.Value, YIsAbsolute = BackgroundPositionY.IsAbsolute,
        };
        set
        {
            BackgroundPositionX = new StyleBoxPositionComponent(value.X, value.XIsAbsolute);
            BackgroundPositionY = new StyleBoxPositionComponent(value.Y, value.YIsAbsolute);
        }
    }

    /// <summary>
    /// Defines how far the background (color, image, or gradient) should extend within an element.
    /// </summary>
    [CssProperty]
    [CssShorthandProperty(BackgroundShorthandName)]
    public StyleBoxBackgroundClip BackgroundClip
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    private StyleBox GetBackground()
    {
        if (BackgroundImage != null)
        {
            return new RfStyleBoxTexture
            {
                Texture = BackgroundImage,
                Modulate = BackgroundColor ?? Color.White,
                Mode = BackgroundRepeat,
                Size = BackgroundSize,
                Position = BackgroundPosition,
                BackgroundClip =  BackgroundClip,
            };
        }

        return new RfStyleBoxFlat
        {
            BackgroundColor = BackgroundColor ?? Color.Transparent,
            BackgroundClip =  BackgroundClip,
        };
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

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var xValue, out var xAbs, isXAxis: true))
            return false;

        x = new StyleBoxPositionComponent(xValue, xAbs);

        if (stream.AtEnd || LooksLikePositionEnd(stream))
        {
            y = new StyleBoxPositionComponent(0.5f, false); // single value: Y defaults to center, per spec
            return true;
        }

        if (!BackgroundPositionParsing.TryParseComponent(ref stream, out var yValue, out var yAbs, isXAxis: false))
            return false;

        y = new StyleBoxPositionComponent(yValue, yAbs);
        return true;
    }

    private static bool LooksLikePositionStart(CssValueStream stream) => stream.Peek() is
        IdentToken { Value: "left" or "right" or "top" or "bottom" or "center" }
        or PercentageToken
        or DimensionToken
        or NumberToken { Value: 0 };

    // Used inside the "background" shorthand, where position parsing must
    // stop at "/" (the size separator) or a non-position token (color etc.),
    // rather than requiring the whole stream to end.
    private static bool LooksLikePositionEnd(CssValueStream stream) =>
        stream.Peek() is DelimToken { CodePoint: '/' } || !LooksLikePositionStart(stream);

    #endregion
}

/// <summary>
/// One axis of a background-position value.
/// </summary>
public record struct StyleBoxPositionComponent(float Value, bool IsAbsolute)
{
    public static readonly StyleBoxPositionComponent Zero = new() { Value = 0f };
}
