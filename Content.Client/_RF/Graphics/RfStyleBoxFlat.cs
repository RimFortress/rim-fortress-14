using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public sealed class RfStyleBoxFlat : RfStyleBox
{
    public Color BackgroundColor { get; set; }

    protected override void DoDrawFill(DrawingHandleScreen handle, UIBox2 box, float uiScale)
    {
        handle.DrawRect(box, BackgroundColor);
    }
}
