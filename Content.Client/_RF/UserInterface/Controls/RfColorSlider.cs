using System.Numerics;
using Content.Client._RF.Suil;
using Content.Client._RF.Graphics;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

/// <summary>
/// A <see cref="RfSlider"/> whose track shows the color it chooses (the red channel, the hue, ...), the SUIL version
/// of <see cref="ColorableSlider"/> with <see cref="ColorSelectorStyleBox"/>.
/// </summary>
/// <remarks>
/// The gradient is drawn by the <c>RoundedFill</c> shader (the gradient of <see cref="ColorSelectorStyleBox"/>) over the padding box of the slider, up
/// to its border, and it is left out where the <c>border-radius</c> cuts the corners off, over its background and under the <see cref="RfSlider.Fill"/>,
/// so a translucent fill tints it. What it shows is set from code through <see cref="Gradient"/>; the border, the
/// radius, <see cref="RfSlider.Grabber"/> and the like come from SUIL.
/// </remarks>
[UsedImplicitly]
public sealed class RfColorSlider : RfSlider
{
    private readonly RoundedFillShader _fill = new();

    /// <summary>
    /// Chooses the colors of the track: <see cref="ColorSelectorStyleBox.ConfigureSlider"/> and
    /// <see cref="ColorSelectorStyleBox.SetBaseColor(Vector4)"/>. It is not drawn itself, only its parameters are used.
    /// </summary>
    [PublicAPI, ViewVariables]
    public ColorSelectorStyleBox Gradient { get; } = new();

    public RfColorSlider()
    {
        MaxValue = 1f;
        AddSuilStyleClass(SuilClass.ColorSlider);
    }

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        // The padding box: inside the border, as the background of the control is.
        var border = Background?.Border;
        var left = (border?.Left.Width ?? 0f) * UIScale;
        var top = (border?.Top.Width ?? 0f) * UIScale;
        var right = (border?.Right.Width ?? 0f) * UIScale;
        var bottom = (border?.Bottom.Width ?? 0f) * UIScale;

        var outer = new UIBox2(0f, 0f, PixelWidth, PixelHeight);
        var box = new UIBox2(left, top, MathF.Max(left, PixelWidth - right), MathF.Max(top, PixelHeight - bottom));
        if (box.Width <= 0f || box.Height <= 0f)
            return;

        // The radii of the border box, reduced by the width of the border.
        var radii = Background is { } background
            ? StyleBoxBorderStroke.ResolveRadii(background.Radius, outer, UIScale)
            : default;
        radii = (Shrink(radii.Tl, left, top), Shrink(radii.Tr, right, top), Shrink(radii.Br, right, bottom), Shrink(radii.Bl, left, bottom));

        var previous = handle.GetShader();
        handle.UseShader(_fill.PrepareGradient(
            handle, box, radii, Gradient.BaseColor, Gradient.XAxis, Gradient.YAxis, Gradient.Hsv));
        handle.DrawRect(box, Color.White);
        handle.UseShader(previous);
    }

    private static Vector2 Shrink(Vector2 radius, float horizontal, float vertical)
        => new(MathF.Max(radius.X - horizontal, 0f), MathF.Max(radius.Y - vertical, 0f));
}
