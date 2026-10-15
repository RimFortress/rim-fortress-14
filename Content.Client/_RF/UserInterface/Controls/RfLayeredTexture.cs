using Content.Suil;
using System.Numerics;
using Content.Client._RF.Suil;
using JetBrains.Annotations;
using Robust.Client.Graphics;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// Several textures drawn over each other and tinted one by one, the <see cref="UiElement"/> version of
/// <see cref="Robust.Client.UserInterface.Controls.LayeredTextureRect"/> (with the colors it doesn't have).
/// </summary>
/// <remarks>
/// The control is a box as any other (background, border, padding); the textures are fitted into its content box,
/// keeping their aspect ratio, and centered. The first texture is the lowest.
/// </remarks>
[Virtual, UsedImplicitly]
[SuilTag("layered-texture")]
public class RfLayeredTexture : UiElement
{
    /// <summary>
    /// The textures to draw, the lowest first.
    /// </summary>
    [PublicAPI]
    public IReadOnlyList<Texture> Textures
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    } = [];

    /// <summary>
    /// The color of every texture of <see cref="Textures"/>, white where there is none.
    /// </summary>
    [PublicAPI]
    public IReadOnlyList<Color> Colors { get; set; } = [];

    /// <summary>
    /// Multiplies the size the control asks for, which is the size of the biggest texture.
    /// </summary>
    [PublicAPI]
    public Vector2 TextureScale
    {
        get;
        set
        {
            field = value;
            InvalidateMeasure();
        }
    } = Vector2.One;

    protected override Vector2 MeasureOverride(Vector2 availableSize)
    {
        var inset = ContentInset;
        var largest = Vector2.Zero;
        foreach (var texture in Textures)
        {
            largest = Vector2.Max(largest, new Vector2(texture.Width, texture.Height));
        }

        return largest * TextureScale + new Vector2(inset.Left + inset.Right, inset.Top + inset.Bottom);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var inset = ContentInset;
        var content = new UIBox2(
            inset.Left * UIScale,
            inset.Top * UIScale,
            MathF.Max(inset.Left * UIScale, PixelWidth - inset.Right * UIScale),
            MathF.Max(inset.Top * UIScale, PixelHeight - inset.Bottom * UIScale));

        if (content.Width <= 0f || content.Height <= 0f)
            return;

        var largest = Vector2.Zero;
        foreach (var texture in Textures)
        {
            largest = Vector2.Max(largest, new Vector2(texture.Width, texture.Height));
        }

        if (largest.X <= 0f || largest.Y <= 0f)
            return;

        // All layers are scaled the same way, so they stay aligned with each other.
        var scale = MathF.Min(content.Width / largest.X, content.Height / largest.Y);
        var center = content.Center;

        for (var i = 0; i < Textures.Count; i++)
        {
            var texture = Textures[i];
            var size = new Vector2(texture.Width, texture.Height) * scale;
            var color = i < Colors.Count ? Colors[i] : Color.White;
            handle.DrawTextureRect(texture, UIBox2.FromDimensions(center - size / 2f, size), color);
        }
    }
}
