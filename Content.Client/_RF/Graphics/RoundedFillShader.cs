using System.Numerics;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Graphics;

/// <summary>
/// The <c>RoundedFill</c> shader: it draws a fill (a color, or the gradient of a color picker) only inside the rounded
/// corners of <c>border-radius</c>. Draw the fill with the shader that <see cref="Prepare"/> or
/// <see cref="PrepareGradient"/> returns, as a rectangle of the same <c>box</c>.
/// </summary>
/// <remarks>
/// The shader keeps its parameters until the frame is drawn, so every fill needs its own instance of this class.
/// </remarks>
public sealed class RoundedFillShader
{
    public static readonly ProtoId<ShaderPrototype> Id = "RoundedFill";

    private readonly ShaderInstance _shader = IoCManager.Resolve<IPrototypeManager>().Index(Id).InstanceUnique();

    public static bool IsRounded((Vector2 Tl, Vector2 Tr, Vector2 Br, Vector2 Bl) radii)
        => Rounded(radii.Tl) || Rounded(radii.Tr) || Rounded(radii.Br) || Rounded(radii.Bl);

    private static bool Rounded(Vector2 r) => r is { X: > 0f, Y: > 0f };

    /// <summary>
    /// The shader for the texture or the color that is drawn into <paramref name="box"/> with the given corner radii.
    /// </summary>
    public ShaderInstance Prepare(
        DrawingHandleScreen handle,
        UIBox2 box,
        (Vector2 Tl, Vector2 Tr, Vector2 Br, Vector2 Bl) radii)
    {
        SetBox(handle, box, radii);
        _shader.SetParameter("gradient", false);
        return _shader;
    }

    /// <summary>
    /// The shader for the gradient of <see cref="ColorSelectorStyleBox"/> that is drawn into <paramref name="box"/>.
    /// </summary>
    public ShaderInstance PrepareGradient(
        DrawingHandleScreen handle,
        UIBox2 box,
        (Vector2 Tl, Vector2 Tr, Vector2 Br, Vector2 Bl) radii,
        Vector4 baseColor,
        Vector4 xAxis,
        Vector4 yAxis,
        bool hsv)
    {
        SetBox(handle, box, radii);
        _shader.SetParameter("gradient", true);
        _shader.SetParameter("baseColor", baseColor);
        _shader.SetParameter("xAxis", xAxis);
        _shader.SetParameter("yAxis", yAxis);
        _shader.SetParameter("hsv", hsv);
        return _shader;
    }

    private void SetBox(DrawingHandleScreen handle, UIBox2 box, (Vector2 Tl, Vector2 Tr, Vector2 Br, Vector2 Bl) radii)
    {
        _shader.SetParameter("size", box.Size);
        _shader.SetParameter("offset", Vector2.Transform(default, handle.GetTransform()) + box.TopLeft);
        _shader.SetParameter("radiiX", new Vector4(radii.Tl.X, radii.Tr.X, radii.Br.X, radii.Bl.X));
        _shader.SetParameter("radiiY", new Vector4(radii.Tl.Y, radii.Tr.Y, radii.Br.Y, radii.Bl.Y));
    }
}
