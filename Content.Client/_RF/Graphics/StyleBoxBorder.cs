using System.Numerics;
using Content.Client._RF.CSS;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._RF.Graphics;

public sealed class StyleBoxBorder(
    CornerRadius radius,
    BorderSide top,
    BorderSide right,
    BorderSide bottom,
    BorderSide left)
{
    public readonly BorderSide Top = top;
    public readonly BorderSide Right = right;
    public readonly BorderSide Bottom = bottom;
    public readonly BorderSide Left = left;
    public readonly CornerRadius Radius = radius;

    private static readonly ShaderInstance DashedShader = Shader("DashedBorder");
    private static readonly ShaderInstance DottedShader = Shader("DottedBorder");
    private static readonly ShaderInstance DoubleShader = Shader("DoubleBorder");
    private static readonly ShaderInstance BevelShader = Shader("BevelBorder");

    private static ShaderInstance Shader(string id) => IoCManager
        .Resolve<IPrototypeManager>()
        .Index<ShaderPrototype>(id)
        .InstanceUnique();

    public void Draw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var t = new Thickness(Left.Width, Top.Width, Right.Width, Bottom.Width).Scale(uiScale);

        var rawTl = ResolveCornerRadius(Radius.TopLeft, box, uiScale);
        var rawTr = ResolveCornerRadius(Radius.TopRight, box, uiScale);
        var rawBr = ResolveCornerRadius(Radius.BottomRight, box, uiScale);
        var rawBl = ResolveCornerRadius(Radius.BottomLeft, box, uiScale);

        var (rTl, rTr, rBr, rBl) = ClampRadii(rawTl, rawTr, rawBr, rawBl, box.Width, box.Height);

        if (t.Top > 0)
            DrawSide(handle, TopQuad(box, t, rTl, rTr), Top, t.Top);

        if (t.Right > 0)
            DrawSide(handle, RightQuad(box, t, rTr, rBr), Right, t.Right);

        if (t.Bottom > 0)
            DrawSide(handle, BottomQuad(box, t, rBr, rBl), Bottom, t.Bottom);

        if (t.Left > 0)
            DrawSide(handle, LeftQuad(box, t, rBl, rTl), Left, t.Left);

        DrawCorner(
            handle,
            new Vector2(box.Left + rTl.X, box.Top + rTl.Y),
            rTl,
            InnerRadius(rTl, t.Left, t.Top),
            MathF.PI,
            MathF.PI * 1.5f,
            Left,
            Top);
        DrawCorner(
            handle,
            new Vector2(box.Right - rTr.X, box.Top + rTr.Y),
            rTr,
            InnerRadius(rTr, t.Right, t.Top),
            MathF.PI * 1.5f,
            MathF.PI * 2f,
            Top,
            Right);
        DrawCorner(
            handle,
            new Vector2(box.Right - rBr.X, box.Bottom - rBr.Y),
            rBr,
            InnerRadius(rBr, t.Right, t.Bottom),
            0f,
            MathF.PI * 0.5f,
            Right,
            Bottom);
        DrawCorner(
            handle,
            new Vector2(box.Left + rBl.X, box.Bottom - rBl.Y),
            rBl,
            InnerRadius(rBl, t.Left, t.Bottom),
            MathF.PI * 0.5f,
            MathF.PI,
            Bottom,
            Left);
    }

    /// <summary>
    /// CSS border-radius clamping: if the two radii sharing an edge would
    /// sum to more than that edge's length, every corner radius on that axis
    /// is scaled down by the same factor — per spec, this keeps opposing
    /// corners from overlapping ("blooming") instead of letting their arcs
    /// pass through each other.
    /// </summary>
    private static (Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl) ClampRadii(
        Vector2 tl,
        Vector2 tr,
        Vector2 br,
        Vector2 bl,
        float width,
        float height)
    {
        var fTop = width / MathF.Max(tl.X + tr.X, 0.0001f);
        var fBottom = width / MathF.Max(bl.X + br.X, 0.0001f);
        var fLeft = height / MathF.Max(tl.Y + bl.Y, 0.0001f);
        var fRight = height / MathF.Max(tr.Y + br.Y, 0.0001f);

        var f = MathF.Min(1f, MathF.Min(MathF.Min(fTop, fBottom), MathF.Min(fLeft, fRight)));
        return f >= 1f ? (tl, tr, br, bl) : (tl * f, tr * f, br * f, bl * f);
    }

    private static bool IsRounded(Vector2 r) => r is { X: > 0f, Y: > 0f };

    // ── Side quads: each end independently branches on whether ITS corner
    // is rounded (arc-start point, offset by that corner's own radius) or
    // sharp (miter point, offset by the perpendicular neighbor's own
    // thickness). These are genuinely different formulas — a sharp corner
    // with unequal adjacent thicknesses needs the neighbor's thickness for
    // its offset, which a "radius=0" substitution into the rounded formula
    // cannot produce (it would collapse both to the bare box corner instead).

    private static SideQuad TopQuad(UIBox2 box, Thickness t, Vector2 rTl, Vector2 rTr)
    {
        var outerA = new Vector2(box.Left + rTl.X, box.Top);
        var outerB = new Vector2(box.Right - rTr.X, box.Top);

        var innerA = IsRounded(rTl)
            ? new Vector2(box.Left + rTl.X, box.Top + (rTl.Y - InnerRadius(rTl, t.Left, t.Top).Y))
            : new Vector2(box.Left + t.Left, box.Top + t.Top);

        var innerB = IsRounded(rTr)
            ? new Vector2(box.Right - rTr.X, box.Top + (rTr.Y - InnerRadius(rTr, t.Right, t.Top).Y))
            : new Vector2(box.Right - t.Right, box.Top + t.Top);

        return new SideQuad(outerA, outerB, innerB, innerA);
    }

    private static SideQuad RightQuad(UIBox2 box, Thickness t, Vector2 rTr, Vector2 rBr)
    {
        var outerA = new Vector2(box.Right, box.Top + rTr.Y);
        var outerB = new Vector2(box.Right, box.Bottom - rBr.Y);

        var innerA = IsRounded(rTr)
            ? new Vector2(box.Right - (rTr.X - InnerRadius(rTr, t.Right, t.Top).X), box.Top + rTr.Y)
            : new Vector2(box.Right - t.Right, box.Top + t.Top);

        var innerB = IsRounded(rBr)
            ? new Vector2(box.Right - (rBr.X - InnerRadius(rBr, t.Right, t.Bottom).X), box.Bottom - rBr.Y)
            : new Vector2(box.Right - t.Right, box.Bottom - t.Bottom);

        return new SideQuad(outerA, outerB, innerB, innerA);
    }

    private static SideQuad BottomQuad(UIBox2 box, Thickness t, Vector2 rBr, Vector2 rBl)
    {
        var outerA = new Vector2(box.Right - rBr.X, box.Bottom);
        var outerB = new Vector2(box.Left + rBl.X, box.Bottom);

        var innerA = IsRounded(rBr)
            ? new Vector2(box.Right - rBr.X, box.Bottom - (rBr.Y - InnerRadius(rBr, t.Right, t.Bottom).Y))
            : new Vector2(box.Right - t.Right, box.Bottom - t.Bottom);

        var innerB = IsRounded(rBl)
            ? new Vector2(box.Left + rBl.X, box.Bottom - (rBl.Y - InnerRadius(rBl, t.Left, t.Bottom).Y))
            : new Vector2(box.Left + t.Left, box.Bottom - t.Bottom);

        return new SideQuad(outerA, outerB, innerB, innerA);
    }

    private static SideQuad LeftQuad(UIBox2 box, Thickness t, Vector2 rBl, Vector2 rTl)
    {
        var outerA = new Vector2(box.Left, box.Bottom - rBl.Y);
        var outerB = new Vector2(box.Left, box.Top + rTl.Y);

        var innerA = IsRounded(rBl)
            ? new Vector2(box.Left + (rBl.X - InnerRadius(rBl, t.Left, t.Bottom).X), box.Bottom - rBl.Y)
            : new Vector2(box.Left + t.Left, box.Bottom - t.Bottom);

        var innerB = IsRounded(rTl)
            ? new Vector2(box.Left + (rTl.X - InnerRadius(rTl, t.Left, t.Top).X), box.Top + rTl.Y)
            : new Vector2(box.Left + t.Left, box.Top + t.Top);

        return new SideQuad(outerA, outerB, innerB, innerA);
    }

    private static void DrawSide(DrawingHandleScreen handle, SideQuad quad, BorderSide sideData, float thickness)
    {
        if (sideData.Style is CssBorderStyle.None or CssBorderStyle.Hidden)
            return;

        var sideLength = (quad.OuterB - quad.OuterA).Length();
        var prev = handle.GetShader();

        switch (sideData.Style)
        {
            case CssBorderStyle.Solid:
                handle.DrawPrimitives(
                    DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    sideData.Color);
                return;
            case CssBorderStyle.Dashed:
                var segment = thickness * 2.5f;
                var gap = segment * 0.6f;
                var period = segment + gap;

                if (sideLength < period)
                {
                    handle.DrawPrimitives(
                        DrawPrimitiveTopology.TriangleFan,
                        new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                        sideData.Color);
                    return;
                }

                DashedShader.SetParameter("side_length", sideLength);
                DashedShader.SetParameter("segment_length", segment);
                DashedShader.SetParameter("gap_length", gap);
                handle.UseShader(DashedShader);
                break;
            case CssBorderStyle.Dotted:
                period = thickness * 2.0f;
                var cornerFade = thickness * 1.5f;

                if (sideLength < period + cornerFade * 2f)
                {
                    handle.DrawPrimitives(
                        DrawPrimitiveTopology.TriangleFan,
                        new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                        sideData.Color);
                    return;
                }

                DottedShader.SetParameter("side_length", sideLength);
                DottedShader.SetParameter("thickness", thickness);
                DottedShader.SetParameter("corner_fade", cornerFade);
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
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, quad.ToVertices(), Color.White);
                return;
            case CssBorderStyle.Inset:
                handle.DrawPrimitives(
                    DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    GetShades(sideData.Color).Dark);
                return;
            case CssBorderStyle.Outset:
                handle.DrawPrimitives(
                    DrawPrimitiveTopology.TriangleFan,
                    new[] { quad.OuterA, quad.OuterB, quad.InnerB, quad.InnerA },
                    GetShades(sideData.Color).Light);
                return;
        }

        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleFan, Texture.White, quad.ToVertices(), sideData.Color);
        handle.UseShader(prev);
    }

    private static void DrawCorner(
        DrawingHandleScreen handle,
        Vector2 center,
        Vector2 outerR,
        Vector2 innerR,
        float startAngle,
        float endAngle,
        BorderSide before,
        BorderSide after)
    {
        if (outerR.X <= 0f || outerR.Y <= 0f)
            return;

        var mid = (startAngle + endAngle) * 0.5f;
        DrawWedgeHalf(handle, center, outerR, innerR, startAngle, mid, before);
        DrawWedgeHalf(handle, center, outerR, innerR, mid, endAngle, after);
    }

    /// <summary>
    /// Draws one half of a rounded corner's wedge for a single side's style.
    /// Unlike <see cref="DrawSide"/>, there is no shared post-switch fallback
    /// here — Double needs the side's own color modulated onto its masked
    /// shader output, while Groove/Ridge need neutral white modulation since
    /// their shader already outputs the final color itself; conflating the
    /// two behind one shared line was the source of a real bug (Double
    /// rendering white at rounded corners) and is kept fully separated per
    /// case instead.
    /// </summary>
    private static void DrawWedgeHalf(
        DrawingHandleScreen handle,
        Vector2 center,
        Vector2 outerR,
        Vector2 innerR,
        float a0,
        float a1,
        BorderSide side)
    {
        if (side.Style is CssBorderStyle.None or CssBorderStyle.Hidden)
            return;

        var verts = CornerWedge.Build(center, outerR, innerR, a0, a1);
        var prev = handle.GetShader();

        switch (side.Style)
        {
            case CssBorderStyle.Solid:
            // A true curved dash/dot pattern around the arc isn't implemented
            // (needs perimeter-arc-length unwrapping — a separately scoped,
            // larger task) — falls back to a solid fill in the side's own
            // color so the border no longer vanishes at the corner, even
            // though the pattern doesn't continue around it.
            case CssBorderStyle.Dashed:
            case CssBorderStyle.Dotted:
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, verts, side.Color);
                return;
            case CssBorderStyle.Double:
                handle.UseShader(DoubleShader);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, verts, side.Color);
                handle.UseShader(prev);
                return;
            case CssBorderStyle.Groove:
            case CssBorderStyle.Ridge:
                var (dark, light) = GetShades(side.Color);
                var (outer, inner) = side.Style == CssBorderStyle.Groove ? (dark, light) : (light, dark);
                BevelShader.SetParameter("outer_color", new Vector4(outer.R, outer.G, outer.B, outer.A));
                BevelShader.SetParameter("inner_color", new Vector4(inner.R, inner.G, inner.B, inner.A));
                handle.UseShader(BevelShader);
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, verts, Color.White);
                handle.UseShader(prev);
                return;
            case CssBorderStyle.Inset:
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, verts, GetShades(side.Color).Dark);
                return;
            case CssBorderStyle.Outset:
                handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, Texture.White, verts, GetShades(side.Color).Light);
                return;
        }
    }

    private static Vector2 ResolveCornerRadius(UnitVector2 radius, UIBox2 borderBox, float uiScale)
        => new(ResolveComponent(radius.X, borderBox.Width / uiScale) * uiScale, ResolveComponent(radius.Y, borderBox.Height / uiScale) * uiScale);

    // referenceSize is in virtual pixels, like the result.
    private static float ResolveComponent(LengthUnit length, float referenceSize) => length.Type switch
    {
        LengthUnitType.Percent => length.PercentToPx(referenceSize).Value,
        LengthUnitType.Calc => length.Calc!.Eval(default, referenceSize), // bound by the control
        _ when length.IsAbsolute => length.AbsoluteToPx().Value,
        _ => throw new NotSupportedException($"border-radius: {length.Type} is not supported here."),
    };

    private static Vector2 InnerRadius(Vector2 outer, float horizontalSideWidth, float verticalSideWidth) => new(
        MathF.Max(outer.X - horizontalSideWidth, 0f),
        MathF.Max(outer.Y - verticalSideWidth, 0f));

    private static (Color Dark, Color Light) GetShades(Color c)
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
/// One triangle-strip-ready arc segment: outer/inner elliptical boundary
/// sampled over an angular sweep, with UV.y = 0 at the outer edge and 1 at
/// the inner — the exact same convention <see cref="SideQuad"/> uses for
/// straight sides, so any shader reading only UV.y (Solid/Double/Groove/
/// Ridge/Inset/Outset) renders correctly here with no changes at all.
/// </summary>
public static class CornerWedge
{
    private const int Segments = 10; // tessellation quality — tune visually

    public static DrawVertexUV2D[] Build(Vector2 center, Vector2 outerRadius, Vector2 innerRadius, float startAngle, float endAngle)
    {
        var verts = new DrawVertexUV2D[(Segments + 1) * 2];

        for (var i = 0; i <= Segments; i++)
        {
            var t = (float)i / Segments;
            var angle = startAngle + (endAngle - startAngle) * t;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));

            var outer = center + dir * outerRadius;
            var inner = center + dir * innerRadius;

            verts[i * 2] = new DrawVertexUV2D(outer, new Vector2(t, 0f));
            verts[i * 2 + 1] = new DrawVertexUV2D(inner, new Vector2(t, 1f));
        }

        return verts;
    }
}

/// <summary>
/// Per-side border data — style, color, and this side's own thickness.
/// </summary>
public readonly record struct BorderSide(CssBorderStyle Style, Color Color, float Width);

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
