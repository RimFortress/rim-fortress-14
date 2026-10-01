using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public sealed class RfStyleBoxFlat : RfStyleBox
{
    public Color BackgroundColor { get; set; }

    protected override void DoDrawFill(DrawingHandleScreen handle, UIBox2 clipBox, UIBox2 originBox, float uiScale)
    {
        handle.DrawRect(clipBox, BackgroundColor);
    }
}
