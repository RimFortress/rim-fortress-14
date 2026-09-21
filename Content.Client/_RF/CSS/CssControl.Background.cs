using Content.Client._RF.Graphics;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
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
    public RfStyleBoxTexture.StretchMode BackgroundRepeat
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Specifies the size of the background images.
    /// There are four different syntaxes you can use with this property:
    /// the keyword syntax ("auto", "cover" and "contain"), the one-value syntax
    /// (sets the width of the image (height becomes "auto"), the two-value syntax
    /// (first value: width of the image, second value: height),
    /// and the multiple background syntax (separated with comma).
    /// </summary>
    public RfStyleBoxTexture.StyleBoxSize BackgroundSize
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Horizontal component of the background image's starting position.
    /// </summary>
    public StyleBoxPositionComponent BackgroundPositionX
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Vertical component of the background image's starting position.
    /// </summary>
    public StyleBoxPositionComponent BackgroundPositionY
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

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
}

/// <summary>
/// One axis of a background-position value.
/// </summary>
public record struct StyleBoxPositionComponent(float Value, bool IsAbsolute)
{
    public static readonly StyleBoxPositionComponent Zero = new() { Value = 0f };
}
