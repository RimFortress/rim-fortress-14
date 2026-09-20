using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public abstract class RfStyleBox : StyleBox
{
    /// <summary>
    /// Which box background painting is confined to. See <see cref="StyleBoxBackgroundClip"/>.
    /// </summary>
    public StyleBoxBackgroundClip BackgroundClip { get; set; } = StyleBoxBackgroundClip.PaddingBox;

    protected RfStyleBox()
    {
    }

    protected RfStyleBox(RfStyleBox copy) : base(copy)
    {
        BackgroundClip = copy.BackgroundClip;
    }

    /// <summary>
    /// Applies <see cref="BackgroundClip"/> to the padding-inset box a
    /// <see cref="StyleBox.DoDraw"/> override receives, returning the box background
    /// painting should actually be confined to.
    /// </summary>
    protected UIBox2 GetClippedBox(UIBox2 paddingBox) => BackgroundClip switch
    {
        StyleBoxBackgroundClip.BorderBox => new UIBox2(paddingBox.Left - PaddingLeft,
            paddingBox.Top - PaddingTop,
            paddingBox.Right + PaddingRight,
            paddingBox.Bottom + PaddingBottom),
        StyleBoxBackgroundClip.ContentBox => new UIBox2(
            paddingBox.Left + (GetContentMargin(Margin.Left) - GetPadding(Margin.Left)),
            paddingBox.Top + (GetContentMargin(Margin.Top) - GetPadding(Margin.Top)),
            paddingBox.Right - (GetContentMargin(Margin.Right) - GetPadding(Margin.Right)),
            paddingBox.Bottom - (GetContentMargin(Margin.Bottom) - GetPadding(Margin.Bottom))),
        _ => paddingBox,
    };
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

    /// <summary>
    /// Paint within the content box (padding-inset box further inset by content margins).
    /// </summary>
    ContentBox,
}
