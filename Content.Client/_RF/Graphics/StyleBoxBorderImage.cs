using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

/// <summary>
/// How the edge regions of a <c>border-image</c> fill the space between its corners.
/// </summary>
public enum BorderImageRepeatMode : byte
{
    /// <summary>
    /// The region is stretched to fill the space.
    /// </summary>
    Stretch,

    /// <summary>
    /// The region is tiled at its natural scale, centered and clipped at both ends.
    /// </summary>
    Repeat,

    /// <summary>
    /// The region is tiled and scaled so that a whole number of tiles fits.
    /// </summary>
    Round,

    /// <summary>
    /// The region is tiled at its natural scale and the tiles are spread out so that a whole number of them fits.
    /// </summary>
    Space,
}

/// <summary>
/// What a <see cref="BorderImageEdge"/> value means.
/// </summary>
public enum BorderImageEdgeKind : byte
{
    /// <summary>
    /// A unitless number: image pixels for a slice, a multiple of the border width for a width and an outset.
    /// </summary>
    Number,

    /// <summary>
    /// A length in virtual pixels.
    /// </summary>
    Length,

    /// <summary>
    /// A percentage (100 is the whole reference size).
    /// </summary>
    Percent,

    /// <summary>
    /// <c>auto</c>: the size of the matching image slice.
    /// </summary>
    Auto,
}

/// <summary>
/// One side of <c>border-image-slice</c>, <c>border-image-width</c> or <c>border-image-outset</c>, with the
/// units already resolved to what <see cref="StyleBoxBorderImage"/> understands.
/// </summary>
public readonly record struct BorderImageEdge(BorderImageEdgeKind Kind, float Value)
{
    public static BorderImageEdge Number(float value) => new(BorderImageEdgeKind.Number, value);

    public static BorderImageEdge Percent(float value) => new(BorderImageEdgeKind.Percent, value);

    public static readonly BorderImageEdge Auto = new(BorderImageEdgeKind.Auto, 0f);
}

/// <summary>
/// Four <see cref="BorderImageEdge"/> values.
/// </summary>
public readonly record struct BorderImageEdges(
    BorderImageEdge Top,
    BorderImageEdge Right,
    BorderImageEdge Bottom,
    BorderImageEdge Left)
{
    public BorderImageEdges(BorderImageEdge all) : this(all, all, all, all)
    {
    }
}

/// <summary>
/// Draws a <c>border-image</c>: the image is cut into nine regions by the slice, the corners are drawn
/// at the size of the border-image-width, and the edges are stretched or tiled between them.
/// </summary>
public sealed class StyleBoxBorderImage(
    CornerRadius radius,
    BorderSide top,
    BorderSide right,
    BorderSide bottom,
    BorderSide left) : StyleBoxBorder(radius, top, right, bottom, left)
{
    // A tile smaller than this many pixels would only produce an absurd number of draw calls.
    private const int MaxTiles = 1024;

    public required Texture Texture { get; init; }

    /// <summary>
    /// <c>border-image-slice</c>: image pixels or percentages of the image size.
    /// </summary>
    public BorderImageEdges Slice { get; init; } = new(BorderImageEdge.Percent(100f));

    /// <summary>
    /// <c>border-image-slice: fill</c>: the middle region is drawn over the padding box.
    /// </summary>
    public bool Fill { get; init; }

    /// <summary>
    /// <c>border-image-width</c>.
    /// </summary>
    public BorderImageEdges Width { get; init; } = new(BorderImageEdge.Number(1f));

    /// <summary>
    /// <c>border-image-outset</c>.
    /// </summary>
    public BorderImageEdges Outset { get; init; } = new(BorderImageEdge.Number(0f));

    public BorderImageRepeatMode RepeatX { get; init; }

    public BorderImageRepeatMode RepeatY { get; init; }

    public override void Draw(DrawingHandleScreen handle, UIBox2 borderBox, float uiScale)
    {
        var border = GetThickness(uiScale);

        var texW = (float) Texture.Width;
        var texH = (float) Texture.Height;

        if (texW <= 0f || texH <= 0f)
            return;

        var area = new UIBox2(
            borderBox.Left - ResolveOutset(Outset.Left, border.Left, uiScale),
            borderBox.Top - ResolveOutset(Outset.Top, border.Top, uiScale),
            borderBox.Right + ResolveOutset(Outset.Right, border.Right, uiScale),
            borderBox.Bottom + ResolveOutset(Outset.Bottom, border.Bottom, uiScale));

        // Not `<=`: a NaN size is not drawn either.
        if (!(area.Width > 0f) || !(area.Height > 0f))
            return;

        // Slice, in image pixels.
        var sT = ResolveSlice(Slice.Top, texH);
        var sR = ResolveSlice(Slice.Right, texW);
        var sB = ResolveSlice(Slice.Bottom, texH);
        var sL = ResolveSlice(Slice.Left, texW);

        // Width, in pixels.
        var wT = ResolveWidth(Width.Top, border.Top, area.Height, sT, uiScale);
        var wR = ResolveWidth(Width.Right, border.Right, area.Width, sR, uiScale);
        var wB = ResolveWidth(Width.Bottom, border.Bottom, area.Height, sB, uiScale);
        var wL = ResolveWidth(Width.Left, border.Left, area.Width, sL, uiScale);

        // The borders must not overlap each other.
        var factor = MathF.Min(1f,
            MathF.Min(
                area.Width / MathF.Max(wL + wR, 0.0001f),
                area.Height / MathF.Max(wT + wB, 0.0001f)));
        wT *= factor;
        wR *= factor;
        wB *= factor;
        wL *= factor;

        var innerLeft = MathF.Min(area.Left + wL, area.Right);
        var innerTop = MathF.Min(area.Top + wT, area.Bottom);
        // The rounding of the floats can turn the inner box inside out when the borders take the whole area.
        var innerRight = Math.Clamp(area.Right - wR, innerLeft, area.Right);
        var innerBottom = Math.Clamp(area.Bottom - wB, innerTop, area.Bottom);

        // Corners.
        DrawRegion(handle, new UIBox2(area.Left, area.Top, innerLeft, innerTop), new UIBox2(0, 0, sL, sT), Top.Color);
        DrawRegion(handle, new UIBox2(innerRight, area.Top, area.Right, innerTop), new UIBox2(texW - sR, 0, texW, sT), Top.Color);
        DrawRegion(handle, new UIBox2(area.Left, innerBottom, innerLeft, area.Bottom), new UIBox2(0, texH - sB, sL, texH), Bottom.Color);
        DrawRegion(handle, new UIBox2(innerRight, innerBottom, area.Right, area.Bottom), new UIBox2(texW - sR, texH - sB, texW, texH), Bottom.Color);

        // Slices that overlap (e.g. the initial 100%) leave no middle in the image: there are no edges to draw,
        // only the corners.
        var hasMiddleX = texW - sL - sR > 0f;
        var hasMiddleY = texH - sT - sB > 0f;

        // Edges. The tile of an edge keeps the proportions of its slice in the direction across the edge.
        if (sT > 0f && hasMiddleX)
        {
            DrawEdge(handle,
                new UIBox2(innerLeft, area.Top, innerRight, innerTop),
                new UIBox2(sL, 0, texW - sR, sT),
                (texW - sL - sR) * (wT / sT),
                horizontal: true,
                RepeatX, Top.Color);
        }

        if (sB > 0f && hasMiddleX)
        {
            DrawEdge(handle,
                new UIBox2(innerLeft, innerBottom, innerRight, area.Bottom),
                new UIBox2(sL, texH - sB, texW - sR, texH),
                (texW - sL - sR) * (wB / sB),
                horizontal: true,
                RepeatX, Bottom.Color);
        }

        if (sL > 0f && hasMiddleY)
        {
            DrawEdge(handle,
                new UIBox2(area.Left, innerTop, innerLeft, innerBottom),
                new UIBox2(0, sT, sL, texH - sB),
                (texH - sT - sB) * (wL / sL),
                horizontal: false,
                RepeatY, Left.Color);
        }

        if (sR > 0f && hasMiddleY)
        {
            DrawEdge(handle,
                new UIBox2(innerRight, innerTop, area.Right, innerBottom),
                new UIBox2(texW - sR, sT, texW, texH - sB),
                (texH - sT - sB) * (wR / sR),
                horizontal: false,
                RepeatY, Right.Color);
        }

        if (Fill && hasMiddleX && hasMiddleY)
        {
            DrawRegion(handle,
                new UIBox2(innerLeft, innerTop, innerRight, innerBottom),
                new UIBox2(sL, sT, texW - sR, texH - sB),
                Top.Color);
        }
    }

    private static float ResolveOutset(BorderImageEdge outset, float borderWidth, float uiScale) => outset.Kind switch
    {
        BorderImageEdgeKind.Number => outset.Value * borderWidth,
        BorderImageEdgeKind.Length => outset.Value * uiScale,
        _ => 0f,
    };

    private static float ResolveSlice(BorderImageEdge slice, float size)
    {
        var value = slice.Kind == BorderImageEdgeKind.Percent ? slice.Value / 100f * size : slice.Value;
        return Math.Clamp(value, 0f, size);
    }

    private static float ResolveWidth(
        BorderImageEdge width,
        float borderWidth,
        float areaSize,
        float slice,
        float uiScale)
        => MathF.Max(0f,
            width.Kind switch
            {
                BorderImageEdgeKind.Number => width.Value * borderWidth,
                BorderImageEdgeKind.Length => width.Value * uiScale,
                BorderImageEdgeKind.Percent => width.Value / 100f * areaSize,
                _ => slice * uiScale, // auto
            });

    private void DrawRegion(DrawingHandleScreen handle, UIBox2 dest, UIBox2 src, Color tint)
    {
        if (dest.Width <= 0f || dest.Height <= 0f || src.Width <= 0f || src.Height <= 0f)
            return;

        handle.DrawTextureRectRegion(Texture, dest, src, tint);
    }

    /// <summary>
    /// Draws one edge region into <paramref name="dest"/>, tiling it along the edge.
    /// </summary>
    private void DrawEdge(
        DrawingHandleScreen handle,
        UIBox2 dest,
        UIBox2 src,
        float tileLength,
        bool horizontal,
        BorderImageRepeatMode mode,
        Color tint)
    {
        var length = horizontal ? dest.Width : dest.Height;

        if (length <= 0f || src.Width <= 0f || src.Height <= 0f)
            return;

        if (mode == BorderImageRepeatMode.Stretch || tileLength <= 0f || length / tileLength > MaxTiles)
        {
            DrawRegion(handle, dest, src, tint);
            return;
        }

        float first, step, tile;
        int count;

        switch (mode)
        {
            case BorderImageRepeatMode.Round:
                count = Math.Max(1, (int) MathF.Round(length / tileLength));
                tile = length / count;
                step = tile;
                first = 0f;
                break;
            case BorderImageRepeatMode.Space:
                count = (int) MathF.Floor(length / tileLength);

                if (count == 0)
                    return;

                tile = tileLength;
                var gap = (length - count * tile) / (count + 1);
                step = tile + gap;
                first = gap;
                break;
            default: // Repeat
                count = (int) MathF.Ceiling(length / tileLength);
                tile = tileLength;
                step = tile;
                first = (length - count * tile) / 2f; // <= 0: the first and the last tile are clipped evenly
                break;
        }

        for (var i = 0; i < count; i++)
        {
            var start = first + i * step;
            var end = start + tile;
            var clippedStart = MathF.Max(start, 0f);
            var clippedEnd = MathF.Min(end, length);

            if (clippedEnd <= clippedStart)
                continue;

            var from = (clippedStart - start) / tile;
            var to = (clippedEnd - start) / tile;

            if (horizontal)
            {
                DrawRegion(handle,
                    new UIBox2(dest.Left + clippedStart, dest.Top, dest.Left + clippedEnd, dest.Bottom),
                    new UIBox2(src.Left + src.Width * from, src.Top, src.Left + src.Width * to, src.Bottom),
                    tint);
            }
            else
            {
                DrawRegion(handle,
                    new UIBox2(dest.Left, dest.Top + clippedStart, dest.Right, dest.Top + clippedEnd),
                    new UIBox2(src.Left, src.Top + src.Height * from, src.Right, src.Top + src.Height * to),
                    tint);
            }
        }
    }
}
