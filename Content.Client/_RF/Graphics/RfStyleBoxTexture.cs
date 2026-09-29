using System.Numerics;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Graphics;

/// <summary>
///     Style box based on a 9-patch texture. An image is
///     divided into up to nine regions by splitting the
///     image along each `PatchMargin.` The corner pieces
///     will be drawn once, at their original size, while
///     the `Mode` controls the (up to five) central pieces
///     which can be either stretched or tiled to fill up
///     the space the box is being drawn in.
/// </summary>
public sealed class RfStyleBoxTexture : RfStyleBox
{
    public RfStyleBoxTexture()
    {
        TilingShader = IoCManager.Resolve<IPrototypeManager>().Index(TilingShaderId).InstanceUnique();
    }

    /// <summary>
    /// <see cref="TilingShader"/> prototype ID.
    /// </summary>
    public static readonly ProtoId<ShaderPrototype> TilingShaderId = "TiledTexture";

    /// <summary>
    /// A shader used to render a tiling texture.
    /// </summary>
    public ShaderInstance TilingShader { get; set; }

    /// <summary>
    /// Mirrors CSS background-size. Auto = native size (TextureScale-based,
    /// current default). Cover/Contain preserve aspect ratio relative to the
    /// drawn area. Explicit sets width/height directly (px or % of the area).
    /// </summary>
    public StyleBoxSize Size { get; set; } = StyleBoxSize.Auto;

    /// <summary>
    /// Starting position of a background image.
    /// </summary>
    public StyleBoxPosition Position { get; set; } = StyleBoxPosition.Default;

    /// <summary>
    /// Left expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="TextureScale"/>.
    /// </remarks>
    public float ExpandMarginLeft { get; set; }

    /// <summary>
    /// Top expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="TextureScale"/>.
    /// </remarks>
    public float ExpandMarginTop { get; set; }

    /// <summary>
    /// Bottom expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="TextureScale"/>.
    /// </remarks>
    public float ExpandMarginBottom { get; set; }

    /// <summary>
    /// Right expansion size, in virtual pixels.
    /// </summary>
    /// <remarks>
    /// This expands the size of the area where the patches get drawn. This will cause the drawn texture to
    /// extend beyond the box passed to the <see cref="StyleBox.Draw"/> function. This is not affected by
    /// <see cref="TextureScale"/>.
    /// </remarks>
    public float ExpandMarginRight { get; set; }

    public StretchMode Mode { get; set; } = StretchMode.Stretch;

    /// <summary>
    /// Distance of the left patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling this with <see cref="TextureScale"/>.
    /// </summary>
    public float PatchMarginLeft
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");

            field = value;
        }
    }

    /// <summary>
    /// Distance of the right patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling this with <see cref="TextureScale"/>.
    /// </summary>
    public float PatchMarginRight
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");

            field = value;
        }
    }

    /// <summary>
    /// Distance of the top patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling this with <see cref="TextureScale"/>.
    /// </summary>
    public float PatchMarginTop
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");

            field = value;
        }
    }

    /// <summary>
    /// Distance of the bottom patch margin from the image. In texture space.
    /// The size of this patch in virtual pixels can be obtained by scaling this with <see cref="TextureScale"/>.
    /// </summary>
    public float PatchMarginBottom
    {
        get;
        set
        {
            if (value < 0)
                throw new ArgumentOutOfRangeException(nameof(value), value, "Value cannot be less than zero.");

            field = value;
        }
    }

    public Color Modulate { get; set; } = Color.White;

    public Texture? Texture { get; set; }

    /// <summary>
    /// Additional scaling to use when drawing the texture.
    /// </summary>
    public Vector2 TextureScale { get; set; } = Vector2.One;

    [PublicAPI]
    public void SetPatchMargin(Margin margin, float value)
    {
        if ((margin & Margin.Top) != 0)
            PatchMarginTop = value;

        if ((margin & Margin.Bottom) != 0)
            PatchMarginBottom = value;

        if ((margin & Margin.Right) != 0)
            PatchMarginRight = value;

        if ((margin & Margin.Left) != 0)
            PatchMarginLeft = value;
    }

    [PublicAPI]
    public void SetExpandMargin(Margin margin, float value)
    {
        if ((margin & Margin.Top) != 0)
            ExpandMarginTop = value;

        if ((margin & Margin.Bottom) != 0)
            ExpandMarginBottom = value;

        if ((margin & Margin.Right) != 0)
            ExpandMarginRight = value;

        if ((margin & Margin.Left) != 0)
            ExpandMarginLeft = value;
    }

    protected override void DoDrawFill(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        if (Texture == null)
            return;

        box = GetClippedBox(box);
        var left = box.Left - ExpandMarginLeft * uiScale;
        var top = box.Top - ExpandMarginTop * uiScale;
        var right = MathF.Max(left, box.Right + ExpandMarginRight * uiScale);
        var bottom = MathF.Max(top, box.Bottom + ExpandMarginBottom * uiScale);
        box = new UIBox2(left, top, right, bottom);

        var sourceMargin = new Thickness(PatchMarginLeft, PatchMarginTop, PatchMarginRight, PatchMarginBottom);
        ClampMargins(ref sourceMargin.Left, ref sourceMargin.Right, Texture.Width);
        ClampMargins(ref sourceMargin.Top, ref sourceMargin.Bottom, Texture.Height);

        var textureScale = Vector2.Max(TextureScale * uiScale, Vector2.Zero);
        var scaledMargin = new Thickness(sourceMargin.Left * textureScale.X,
            sourceMargin.Top * textureScale.Y,
            sourceMargin.Right * textureScale.X,
            sourceMargin.Bottom * textureScale.Y);
        ClampMargins(ref scaledMargin.Left, ref scaledMargin.Right, box.Width);
        ClampMargins(ref scaledMargin.Top, ref scaledMargin.Bottom, box.Height);

        var innerRight = box.Width - scaledMargin.Right;
        var innerBottom = box.Height - scaledMargin.Bottom;

        if (sourceMargin.Left > 0)
        {
            if (sourceMargin.Top > 0)
            {
                // Draw top left
                var topLeftBox = new UIBox2(0, 0, scaledMargin.Left, scaledMargin.Top)
                    .Translated(box.TopLeft);
                DrawRegion(handle,
                    topLeftBox,
                    new UIBox2(0, 0, sourceMargin.Left, sourceMargin.Top));
            }

            {
                // Draw left
                var leftBox =
                    new UIBox2(0, scaledMargin.Top, scaledMargin.Left, innerBottom)
                        .Translated(box.TopLeft);
                DrawStretchingArea(handle,
                    leftBox,
                    new UIBox2(0, sourceMargin.Top, sourceMargin.Left, Texture.Height - sourceMargin.Bottom),
                    uiScale);
            }

            if (sourceMargin.Bottom > 0)
            {
                // Draw bottom left
                var bottomLeftBox =
                    new UIBox2(0, box.Height - scaledMargin.Bottom, scaledMargin.Left, box.Height)
                        .Translated(box.TopLeft);
                DrawRegion(handle,
                    bottomLeftBox,
                    new UIBox2(0, Texture.Height - sourceMargin.Bottom, sourceMargin.Left, Texture.Height));
            }
        }

        if (sourceMargin.Right > 0)
        {
            if (sourceMargin.Top > 0)
            {
                // Draw top right
                var topRightBox = new UIBox2(box.Width - scaledMargin.Right, 0, box.Width, scaledMargin.Top)
                    .Translated(box.TopLeft);
                DrawRegion(handle,
                    topRightBox,
                    new UIBox2(Texture.Width - sourceMargin.Right, 0, Texture.Width, sourceMargin.Top));
            }

            {
                // Draw right
                var rightBox =
                    new UIBox2(box.Width - scaledMargin.Right,
                            scaledMargin.Top,
                            box.Width,
                            innerBottom)
                        .Translated(box.TopLeft);

                DrawStretchingArea(handle,
                    rightBox,
                    new UIBox2(Texture.Width - sourceMargin.Right,
                        sourceMargin.Top,
                        Texture.Width,
                        Texture.Height - sourceMargin.Bottom),
                    uiScale);
            }

            if (sourceMargin.Bottom > 0)
            {
                // Draw bottom right
                var bottomRightBox =
                    new UIBox2(box.Width - scaledMargin.Right, box.Height - scaledMargin.Bottom, box.Width, box.Height)
                        .Translated(box.TopLeft);
                DrawRegion(handle,
                    bottomRightBox,
                    new UIBox2(Texture.Width - sourceMargin.Right,
                        Texture.Height - sourceMargin.Bottom,
                        Texture.Width,
                        Texture.Height));
            }
        }

        if (sourceMargin.Top > 0)
        {
            // Draw top
            var topBox =
                new UIBox2(scaledMargin.Left, 0, innerRight, scaledMargin.Top)
                    .Translated(box.TopLeft);
            DrawStretchingArea(handle,
                topBox,
                new UIBox2(sourceMargin.Left, 0, Texture.Width - sourceMargin.Right, sourceMargin.Top),
                uiScale);
        }

        if (sourceMargin.Bottom > 0)
        {
            // Draw bottom
            var bottomBox =
                new UIBox2(scaledMargin.Left,
                        box.Height - scaledMargin.Bottom,
                        innerRight,
                        box.Height)
                    .Translated(box.TopLeft);

            DrawStretchingArea(handle,
                bottomBox,
                new UIBox2(sourceMargin.Left,
                    Texture.Height - sourceMargin.Bottom,
                    Texture.Width - sourceMargin.Right,
                    Texture.Height),
                uiScale);
        }

        // Draw center
        {
            var centerBox = new UIBox2(scaledMargin.Left,
                scaledMargin.Top,
                innerRight,
                innerBottom).Translated(box.TopLeft);

            DrawStretchingArea(handle,
                centerBox,
                new UIBox2(sourceMargin.Left,
                    sourceMargin.Top,
                    Texture.Width - sourceMargin.Right,
                    Texture.Height - sourceMargin.Bottom),
                uiScale);
        }
    }

    private void DrawRegion(DrawingHandleScreen handle, UIBox2 area, UIBox2 texCoords)
    {
        if (area.Width <= 0 || area.Height <= 0 || texCoords.Width <= 0 || texCoords.Height <= 0)
            return;

        handle.DrawTextureRectRegion(Texture!, area, texCoords, Modulate);
    }

    private void DrawStretchingArea(DrawingHandleScreen handle, UIBox2 area, UIBox2 texCoords, float uiScale)
    {
        if (area.Width <= 0 || area.Height <= 0 || texCoords.Width <= 0 || texCoords.Height <= 0)
            return;

        switch (Mode)
        {
            case StretchMode.Stretch:
                handle.DrawTextureRectRegion(Texture!, area, texCoords, Modulate);
                return;
            case StretchMode.NoRepeat:
                DrawNoTile(handle, area, texCoords, uiScale);
                return;
            default: // Tile / TileX / TileY
                DrawTiled(handle, area, texCoords, uiScale);
                return;
        }
    }

    private void DrawNoTile(DrawingHandleScreen handle, UIBox2 area, UIBox2 texCoords, float uiScale)
    {
        var naturalSize = ComputeTileSize(area, texCoords, uiScale);
        var clippedSize = Vector2.Min(naturalSize, area.Size);
        var anchor = ComputeAnchor(area, clippedSize, uiScale);
        var destBox = UIBox2.FromDimensions(anchor, clippedSize);

        var texClip = new UIBox2(
            texCoords.Left,
            texCoords.Top,
            texCoords.Left + texCoords.Width * (clippedSize.X / naturalSize.X),
            texCoords.Top + texCoords.Height * (clippedSize.Y / naturalSize.Y));

        handle.DrawTextureRectRegion(Texture!, destBox, texClip, Modulate);
    }

    private void DrawTiled(DrawingHandleScreen handle, UIBox2 area, UIBox2 texCoords, float uiScale)
    {
        if (Texture is null || TextureScale.X <= 0 || TextureScale.Y <= 0 || uiScale <= 0)
            return;

        var tileSize = ComputeTileSize(area, texCoords, uiScale);
        var anchor = ComputeAnchor(area, tileSize, uiScale);

        var repeatX = Mode is StretchMode.Repeat or StretchMode.RepeatX;
        var repeatY = Mode is StretchMode.Repeat or StretchMode.RepeatY;

        var areaSizeParam = new Vector2(
            repeatX ? tileSize.X : area.Width,
            repeatY ? tileSize.Y : area.Height);

        var phaseOffset = new Vector2(
            repeatX ? (anchor.X - area.Left) / tileSize.X : 0f,
            repeatY ? (anchor.Y - area.Top) / tileSize.Y : 0f);

        var texSize = new Vector2(Texture.Width, Texture.Height);
        var regionMin = new Vector2(texCoords.Left / texSize.X, texCoords.Top / texSize.Y);
        var regionMax = new Vector2(texCoords.Right / texSize.X, texCoords.Bottom / texSize.Y);

        TilingShader.SetParameter("area_size", areaSizeParam);
        TilingShader.SetParameter("tex_region_min", regionMin);
        TilingShader.SetParameter("tex_region_max", regionMax);
        TilingShader.SetParameter("phase_offset", phaseOffset);

        var prevShader = handle.GetShader();
        handle.UseShader(TilingShader);
        handle.DrawTextureRectRegion(Texture, area, texCoords, Modulate);
        handle.UseShader(prevShader);
    }

    private static void ClampMargins(ref float start, ref float end, float maxSize)
    {
        if (maxSize <= 0)
        {
            start = 0;
            end = 0;
            return;
        }

        start = Math.Clamp(start, 0, maxSize);
        end = Math.Clamp(end, 0, maxSize);

        var total = start + end;
        if (total <= maxSize)
            return;

        var scale = maxSize / total;
        start *= scale;
        end *= scale;
    }

    protected override float GetDefaultContentMargin(Margin margin) => margin switch
    {
        Margin.Top => PatchMarginTop * TextureScale.Y,
        Margin.Bottom => PatchMarginBottom * TextureScale.Y,
        Margin.Right => PatchMarginRight * TextureScale.X,
        Margin.Left => PatchMarginLeft * TextureScale.X,
        _ => throw new ArgumentOutOfRangeException(nameof(margin), margin, null),
    };

    /// <summary>
    /// The size of one texture repeat/copy after BackgroundSize is applied —
    /// used as the tile size in DrawTiled and the draw size in DrawNoTile.
    /// </summary>
    private Vector2 ComputeTileSize(UIBox2 area, UIBox2 texCoords, float uiScale)
    {
        var natural = texCoords.Size * TextureScale * uiScale;
        if (natural.X <= 0 || natural.Y <= 0)
            return natural;

        switch (Size.Mode)
        {
            case StyleBoxBackgroundSizeMode.Auto:
                return natural;

            case StyleBoxBackgroundSizeMode.Cover:
            {
                var scale = MathF.Max(area.Width / natural.X, area.Height / natural.Y);
                return natural * scale;
            }

            case StyleBoxBackgroundSizeMode.Contain:
            {
                var scale = MathF.Min(area.Width / natural.X, area.Height / natural.Y);
                return natural * scale;
            }

            case StyleBoxBackgroundSizeMode.Explicit:
            {
                var w = Size.WidthIsPercent ? area.Width * Size.Width
                    : Size.WidthAuto ? -1f
                    : Size.Width * uiScale;

                var h = Size.HeightIsPercent ? area.Height * Size.Height
                    : Size.HeightAuto ? -1f
                    : Size.Height * uiScale;

                if (w < 0 && h >= 0)
                    w = natural.X * (h / natural.Y);
                else if (h < 0 && w >= 0)
                    h = natural.Y * (w / natural.X);
                else if (w < 0 && h < 0)
                    return natural;

                return new Vector2(w, h);
            }

            default:
                return natural;
        }
    }

    private Vector2 ComputeAnchor(UIBox2 area, Vector2 tileSize, float uiScale)
    {
        var x = Position.XIsAbsolute
            ? area.Left + Position.X * uiScale
            : area.Left + (area.Width - tileSize.X) * Position.X;

        var y = Position.YIsAbsolute
            ? area.Top + Position.Y * uiScale
            : area.Top + (area.Height - tileSize.Y) * Position.Y;

        return new Vector2(x, y);
    }

    /// <summary>
    /// Specifies how to stretch the sides and center of the style box.
    /// </summary>
    public enum StretchMode : byte
    {
        Stretch,
        Repeat,
        RepeatX,
        RepeatY,
        NoRepeat,
    }

    public enum StyleBoxBackgroundSizeMode
    {
        Auto,
        Cover,
        Contain,
        Explicit,
    }

    public struct StyleBoxSize
    {
        public StyleBoxBackgroundSizeMode Mode;

        public float Width;
        public float Height;
        public bool WidthIsPercent;
        public bool HeightIsPercent;
        public bool WidthAuto;
        public bool HeightAuto;

        public static readonly StyleBoxSize Auto = new() { Mode = StyleBoxBackgroundSizeMode.Auto };
        public static readonly StyleBoxSize Cover = new() { Mode = StyleBoxBackgroundSizeMode.Cover };
        public static readonly StyleBoxSize Contain = new() { Mode = StyleBoxBackgroundSizeMode.Contain };
    }

    public struct StyleBoxPosition
    {
        public float X;
        public float Y;
        public bool XIsAbsolute;
        public bool YIsAbsolute;

        public static readonly StyleBoxPosition Default = new() { X = 0f, Y = 0f }; // left top
    }
}
