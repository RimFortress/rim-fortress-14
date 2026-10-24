using Robust.Client.Graphics;

namespace Content.Client._RF.Graphics;

public sealed class RfStyleBoxFlat : RfStyleBox
{
    private RoundedFillShader? _rounded;

    public Color BackgroundColor { get; set; }

    protected override void DoDrawFill(DrawingHandleScreen handle, UIBox2 clipBox, UIBox2 originBox, float uiScale)
    {
        if (!RoundedFillShader.IsRounded(FillRadii))
        {
            handle.DrawRect(clipBox, BackgroundColor);
            return;
        }

        // The shader leaves out what the corners cut off, as the border does.
        var previous = handle.GetShader();
        handle.UseShader((_rounded ??= new RoundedFillShader()).Prepare(handle, clipBox, FillRadii));
        handle.DrawRect(clipBox, BackgroundColor);
        handle.UseShader(previous);
    }
}
