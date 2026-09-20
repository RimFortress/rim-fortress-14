using Content.Client._RF.Graphics;
using Robust.Client.Graphics;

namespace Content.Client._RF.CSS;

public partial class CssControl
{
    protected StyleBox? Background;

    /// <summary>
    /// Sets the background color of an element. The background of an element is the
    /// total size of the element, including padding and border (but not the margin).
    /// </summary>
    /// <remarks>
    /// If a texture is used as the background, the color will be used to modulate the texture.
    /// </remarks>
    protected Color? BackgroundColor
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
    protected RfStyleBoxTexture.StretchMode BackgroundRepeat
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
    protected RfStyleBoxTexture.StyleBoxSize BackgroundSize
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Sets the starting position of a <see cref="BackgroundImage"/>.
    /// </summary>
    protected RfStyleBoxTexture.StyleBoxPosition BackgroundPosition
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    /// <summary>
    /// Defines how far the background (color, image, or gradient) should extend within an element.
    /// </summary>
    protected StyleBoxBackgroundClip BackgroundClip
    {
        get;
        set
        {
            field = value;
            Background = GetBackground();
        }
    }

    private StyleBox? GetBackground()
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

        if (BackgroundColor == null)
            return null;

        return new RfStyleBoxFlat
        {
            BackgroundColor = BackgroundColor.Value,
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
