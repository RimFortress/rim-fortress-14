using System.Numerics;
using Content.Client._RF.CSS;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Graphics;

/// <summary>
/// Paints a border stroke around a box of the given thickness, split into
/// four independent per-side draw calls (top/right/bottom/left) — mirrors
/// how real CSS treats each border side as its own paintable strip.
/// </summary>
public sealed class StyleBoxBorder(BorderSide top, BorderSide right, BorderSide bottom, BorderSide left)
{
    public readonly BorderSide Top = top;
    public readonly BorderSide Right = right;
    public readonly BorderSide Bottom = bottom;
    public readonly BorderSide Left = left;

    private static readonly ShaderInstance DashedShader = Shader("DashedBorder");
    private static readonly ShaderInstance DottedShader = Shader("DottedBorder");
    private static readonly ShaderInstance DoubleShader = Shader("DoubleBorder");
    private static readonly ShaderInstance BevelShader = Shader("BevelBorder");

    private static ShaderInstance Shader(string id) => IoCManager
        .Resolve<IPrototypeManager>()
        .Index<ShaderPrototype>(id)
        .InstanceUnique();

    private static SideQuad TopQuad(UIBox2 box, Thickness t) => new(
        OuterA: new(box.Left, box.Top),
        OuterB: new(box.Right, box.Top),
        InnerB: new(box.Right - (t.Right > 0 ? t.Right : 0), box.Top + t.Top),
        InnerA: new(box.Left + (t.Left > 0 ? t.Left : 0), box.Top + t.Top));

    private static SideQuad RightQuad(UIBox2 box, Thickness t) => new(
        OuterA: new(box.Right, box.Top),
        OuterB: new(box.Right, box.Bottom),
        InnerB: new(box.Right - t.Right, box.Bottom - (t.Bottom > 0 ? t.Bottom : 0)),
        InnerA: new(box.Right - t.Right, box.Top + (t.Top > 0 ? t.Top : 0)));

    private static SideQuad BottomQuad(UIBox2 box, Thickness t) => new(
        OuterA: new(box.Right, box.Bottom),
        OuterB: new(box.Left, box.Bottom),
        InnerB: new(box.Left + (t.Left > 0 ? t.Left : 0), box.Bottom - t.Bottom),
        InnerA: new(box.Right - (t.Right > 0 ? t.Right : 0), box.Bottom - t.Bottom));

    private static SideQuad LeftQuad(UIBox2 box, Thickness t) => new(
        OuterA: new(box.Left, box.Bottom),
        OuterB: new(box.Left, box.Top),
        InnerB: new(box.Left + t.Left, box.Top + (t.Top > 0 ? t.Top : 0)),
        InnerA: new(box.Left + t.Left, box.Bottom - (t.Bottom > 0 ? t.Bottom : 0)));

    /// <summary>
    /// Draws all four sides of the border within <paramref name="box"/>.
    /// A side with zero (scaled) thickness is skipped entirely — its
    /// per-side draw method is never called.
    /// </summary>
    public void Draw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var t = new Thickness(Left.Width, Top.Width, Right.Width, Bottom.Width).Scale(uiScale);

        if (t.Top > 0)
            DrawSide(handle, TopQuad(box, t), Top, t.Top);

        if (t.Right > 0)
            DrawSide(handle, RightQuad(box, t), Right, t.Right);

        if (t.Bottom > 0)
            DrawSide(handle, BottomQuad(box, t), Bottom, t.Bottom);

        if (t.Left > 0)
            DrawSide(handle, LeftQuad(box, t), Left, t.Left);
    }

    private void DrawSide(DrawingHandleScreen handle, SideQuad quad, BorderSide sideData, float thickness)
    {
        if (sideData.Style is CssBorderStyle.None or CssBorderStyle.Hidden)
            return;

        var sideLength = (quad.OuterB - quad.OuterA).Length();
        var prev = handle.GetShader();

        switch (sideData.Style)
        {
            case CssBorderStyle.Solid:
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    sideData.Color);
                return;
            case CssBorderStyle.Dashed:
                var segment = thickness * 2.5f;
                DashedShader.SetParameter("side_length", sideLength);
                DashedShader.SetParameter("segment_length", segment);
                DashedShader.SetParameter("gap_length", segment * 0.6f);
                handle.UseShader(DashedShader);
                break;
            case CssBorderStyle.Dotted:
                DottedShader.SetParameter("side_length", sideLength);
                DottedShader.SetParameter("thickness", thickness);
                DottedShader.SetParameter("corner_fade", thickness * 1.5f);
                handle.UseShader(DottedShader);
                break;
            case CssBorderStyle.Double:
                handle.UseShader(DoubleShader);
                break;
            case CssBorderStyle.Groove:
            case CssBorderStyle.Ridge:
                var (dark, light) = GetShades(sideData.Color);
                var (outer, inner) = sideData.Style == CssBorderStyle.Groove ? (dark, light) : (light, dark);
                BevelShader.SetParameter("outer_color", new Vector4(outer.R, outer.G, outer.B, outer.A));
                BevelShader.SetParameter("inner_color", new Vector4(inner.R, inner.G, inner.B, inner.A));
                handle.UseShader(BevelShader);
                break;
            case CssBorderStyle.Inset:
                (dark, _) = GetShades(sideData.Color);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    dark);
                return;
            case CssBorderStyle.Outset:
                (_, light) = GetShades(sideData.Color);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    light);
                return;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, quad.ToVertices(), sideData.Color);
        handle.UseShader(prev);
    }

    /// <summary>
    /// CSS doesn't specify the exact shading formula for groove/ridge/inset/
    /// outset — every browser computes it differently. This is a fixed 35%
    /// lighten/darken of the base color, not a spec-mandated value.
    /// </summary>
    private static (Color dark, Color light) GetShades(Color c)
    {
        const float f = 0.35f;
        var dark = new Color(c.R * (1 - f), c.G * (1 - f), c.B * (1 - f), c.A);
        var light = new Color(c.R + (1 - c.R) * f, c.G + (1 - c.G) * f, c.B + (1 - c.B) * f, c.A);
        return (Color.FromSrgb(dark), Color.FromSrgb(light));
    }
}

/// <summary>
/// A border side's paintable quad — a rectangle when both neighboring sides
/// are absent (zero thickness), a trapezoid mitered at 45° toward whichever
/// neighbor(s) are present, matching how CSS border corners meet. Points are
/// given in perimeter order (OuterA → OuterB → InnerB → InnerA) with UV
/// (0,0)/(1,0)/(1,1)/(0,1) assigned respectively — U runs along the side's
/// length, V runs from the outer edge (0) to the inner edge (1) — so a
/// style's shader can compute its pattern from UV alone, whether the quad is
/// trapezoidal or rectangular.
/// </summary>
public readonly record struct SideQuad(Vector2 OuterA, Vector2 OuterB, Vector2 InnerB, Vector2 InnerA)
{
    public DrawVertexUV2D[] ToVertices()
    {
        var dir = OuterB - OuterA;
        var length = dir.Length();
        var dirNorm = length > 0f ? dir / length : Vector2.Zero;
        var outerA = OuterA;

        return
        [
            new(OuterA, new Vector2(U(OuterA), 0)),
            new(OuterB, new Vector2(U(OuterB), 0)),
            new(InnerB, new Vector2(U(InnerB), 1)),
            new(InnerA, new Vector2(U(InnerA), 1)),
        ];

        float U(Vector2 p) => length > 0f ? Vector2.Dot(p - outerA, dirNorm) / length : 0f;
    }
}

/// <summary>
/// Per-side border data — style, color, and this side's own thickness.
/// </summary>
public readonly record struct BorderSide(CssBorderStyle Style, Color Color, float Width);

/* TODO

/// <summary>
/// Per-corner radius for rounding a <see cref="RfStyleBox"/>'s outer edge, mirroring CSS <c>border-radius</c>.
/// </summary>
public readonly record struct CornerRadius(UnitVector2 TopLeft, UnitVector2 TopRight, UnitVector2 BottomRight, UnitVector2 BottomLeft)
{
    /// <summary>
    /// No rounding on any corner — the default, matching a plain rectangular box.
    /// </summary>
    public static readonly CornerRadius Zero = default;

    /// <summary>
    /// True if every corner's radius is zero (no rounding needed at all).
    /// </summary>
    public bool IsZero => TopLeft.IsZero && TopRight.IsZero && BottomRight.IsZero && BottomLeft.IsZero;
}

*/
