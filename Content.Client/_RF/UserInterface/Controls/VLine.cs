using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls;

public sealed class VLine : Container
{
    public Color? Color
    {
        get
        {
            if (_line.PanelOverride is StyleBoxFlat styleBox)
                return styleBox.BackgroundColor;

            return null;
        }
        set
        {
            if (_line.PanelOverride is StyleBoxFlat styleBox)
                styleBox.BackgroundColor = value!.Value;
        }
    }

    public float? Thickness
    {
        get
        {
            if (_line.PanelOverride is StyleBoxFlat styleBox)
                return styleBox.ContentMarginLeftOverride;

            return null;
        }
        set
        {
            if (_line.PanelOverride is StyleBoxFlat styleBox)
                styleBox.ContentMarginLeftOverride = value!.Value;
        }
    }

    private readonly PanelContainer _line;

    public VLine()
    {
        _line = new PanelContainer();
        _line.PanelOverride = new StyleBoxFlat();
        _line.PanelOverride.ContentMarginLeftOverride = Thickness;
        AddChild(_line);
    }
}
