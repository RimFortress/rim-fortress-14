using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public abstract class RfStyleBox : StyleBox
{
    /// <summary>
    /// Which box background painting is confined to. See <see cref="StyleBoxBackgroundClip"/>.
    /// </summary>
    public StyleBoxBackgroundClip BackgroundClip { get; set; } = StyleBoxBackgroundClip.PaddingBox;

    /// <summary>
    /// Border of the background.
    /// </summary>
    public StyleBoxBorder? Border{ get; set; }

    protected abstract void DoDrawFill(DrawingHandleScreen handle, UIBox2 box, float uiScale);

    protected sealed override void DoDraw(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        var borderBox = new UIBox2(
            box.Left - PaddingLeft,
            box.Top - PaddingTop,
            box.Right + PaddingRight,
            box.Bottom + PaddingBottom);

        DoDrawFill(handle, GetClippedBox(borderBox), uiScale);
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
    /// Computes the fill area for a given <see cref="BackgroundClip"/>, from the
    /// box's TRUE outer border edge. BorderBox = unchanged (paints under padding
    /// AND under the border). PaddingBox = inset by the BORDER's own thickness
    /// only — not by Padding; per spec, padding-box's boundary is where the
    /// border ends, regardless of the Padding value itself. ContentBox = inset
    /// by both the border's thickness AND Padding.
    /// </summary>
    protected UIBox2 GetClippedBox(UIBox2 borderBox)
    {
        var bl = Border?.Left.Width ?? 0f;
        var bt = Border?.Top.Width ?? 0f;
        var br = Border?.Right.Width ?? 0f;
        var bb = Border?.Bottom.Width ?? 0f;

        return BackgroundClip switch
        {
            StyleBoxBackgroundClip.BorderBox => borderBox,
            StyleBoxBackgroundClip.ContentBox => new UIBox2(
                borderBox.Left + bl + PaddingLeft,
                borderBox.Top + bt + PaddingTop,
                borderBox.Right - br - PaddingRight,
                borderBox.Bottom - bb - PaddingBottom),
            _ => new UIBox2( // PaddingBox
                borderBox.Left + bl,
                borderBox.Top + bt,
                borderBox.Right - br,
                borderBox.Bottom - bb),
        };
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

    /// <summary>
    /// Paint within the content box (padding-inset box further inset by content margins).
    /// </summary>
    ContentBox,
}
