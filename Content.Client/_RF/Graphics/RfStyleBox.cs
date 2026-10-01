using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public abstract class RfStyleBox : StyleBox
{
    /// <summary>
    /// Which box background painting is confined to. See <see cref="StyleBoxBackgroundClip"/>.
    /// </summary>
    public StyleBoxBackgroundClip BackgroundClip { get; set; } = StyleBoxBackgroundClip.PaddingBox;

    /// <summary>
    /// The reference box for resolving background-position/background-size
    /// percentages and the tiling pattern's starting phase — mirrors CSS
    /// <c>background-origin</c>. Independent of <see cref="BackgroundClip"/>:
    /// origin decides WHERE positioning/sizing math is anchored, clip decides
    /// what's actually painted/visible — the two commonly differ (e.g. a
    /// tiled texture can be positioned from the padding box while still
    /// painting all the way out to the border box).
    /// </summary>
    public StyleBoxBackgroundOrigin BackgroundOrigin { get; set; } = StyleBoxBackgroundOrigin.PaddingBox;

    /// <summary>
    /// Border of the background.
    /// </summary>
    public StyleBoxBorder? Border { get; set; }

    /// <summary>
    /// Paints the fill. <paramref name="clipBox"/> is where painting is
    /// actually visible/confined (per <see cref="BackgroundClip"/>);
    /// <paramref name="originBox"/> is the reference box for resolving
    /// percentages and tiling phase (per <see cref="BackgroundOrigin"/>).
    /// For a flat color fill, only <paramref name="clipBox"/> matters —
    /// origin has no effect on a plain <c>background-color</c>, per spec.
    /// </summary>
    protected abstract void DoDrawFill(DrawingHandleScreen handle, UIBox2 clipBox, UIBox2 originBox, float uiScale);

    protected sealed override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var borderBox = new UIBox2(
            box.Left - PaddingLeft,
            box.Top - PaddingTop,
            box.Right + PaddingRight,
            box.Bottom + PaddingBottom);

        DoDrawFill(handle, GetClippedBox(borderBox), GetOriginBox(borderBox), uiScale);
        Border?.Draw(handle, borderBox, uiScale);
    }

    protected override float GetDefaultContentMargin(Margin margin) => margin switch
    {
        Margin.Top => Border?.Top.Width ?? 0f,
        Margin.Bottom => Border?.Bottom.Width ?? 0f,
        Margin.Right => Border?.Right.Width ?? 0f,
        Margin.Left => Border?.Left.Width ?? 0f,
        _ => throw new ArgumentOutOfRangeException(nameof(margin), margin, null),
    };

    /// <summary>
    /// Computes the fill's painted/visible area for the current
    /// <see cref="BackgroundClip"/>. See remarks on <see cref="DoDrawFill"/>
    /// for how this differs from <see cref="GetOriginBox"/>.
    /// </summary>
    protected UIBox2 GetClippedBox(UIBox2 borderBox) => BackgroundClip switch
    {
        StyleBoxBackgroundClip.BorderBox => borderBox,
        StyleBoxBackgroundClip.ContentBox => InsetBox(borderBox, insetByBorder: true, insetByPadding: true),
        _ => InsetBox(borderBox, insetByBorder: true, insetByPadding: false), // PaddingBox
    };

    /// <summary>
    /// Computes the fill's positioning/sizing reference area for the current
    /// <see cref="BackgroundOrigin"/>. See remarks on <see cref="DoDrawFill"/>.
    /// </summary>
    protected UIBox2 GetOriginBox(UIBox2 borderBox) => BackgroundOrigin switch
    {
        StyleBoxBackgroundOrigin.BorderBox => borderBox,
        StyleBoxBackgroundOrigin.ContentBox => InsetBox(borderBox, insetByBorder: true, insetByPadding: true),
        _ => InsetBox(borderBox, insetByBorder: true, insetByPadding: false), // PaddingBox
    };

    private UIBox2 InsetBox(UIBox2 borderBox, bool insetByBorder, bool insetByPadding)
    {
        var bl = insetByBorder ? Border?.Left.Width ?? 0f : 0f;
        var bt = insetByBorder ? Border?.Top.Width ?? 0f : 0f;
        var br = insetByBorder ? Border?.Right.Width ?? 0f : 0f;
        var bb = insetByBorder ? Border?.Bottom.Width ?? 0f : 0f;

        var pl = insetByPadding ? PaddingLeft : 0f;
        var pt = insetByPadding ? PaddingTop : 0f;
        var pr = insetByPadding ? PaddingRight : 0f;
        var pb = insetByPadding ? PaddingBottom : 0f;

        return new UIBox2(
            borderBox.Left + bl + pl,
            borderBox.Top + bt + pt,
            borderBox.Right - br - pr,
            borderBox.Bottom - bb - pb);
    }
}

/// <summary>
/// Mirrors CSS background-clip: which box the background is painted within,
/// relative to the padding and the content margins.
/// </summary>
public enum StyleBoxBackgroundClip : byte
{
    /// <summary>
    /// Paint out to the box originally passed to <see cref="StyleBox.Draw"/>, ignoring padding.
    /// </summary>
    BorderBox,

    /// <summary>
    /// Paint within the padding-inset box (default; matches prior behavior).
    /// </summary>
    PaddingBox,

    ContentBox,
}

/// <summary>
/// Mirrors CSS background-origin: the reference box for resolving
/// background-position/background-size percentages and where a tiled
/// pattern's phase starts counting from zero. Has no visible effect on
/// <see cref="RfStyleBoxFlat"/> (a flat color has no position/size to
/// anchor) — only matters for <see cref="RfStyleBoxTexture"/>.
/// </summary>
public enum StyleBoxBackgroundOrigin : byte
{
    /// <summary>
    /// Position/size/tiling phase are relative to the outer border edge.
    /// </summary>
    BorderBox,

    /// <summary>
    /// Position/size/tiling phase are relative to where the border ends (default, matches CSS).
    /// </summary>
    PaddingBox,

    /// <summary>
    /// Position/size/tiling phase are relative to where padding ends.
    /// </summary>
    ContentBox,
}
