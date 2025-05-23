using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._RF.World.UI;

[GenerateTypedNameReferences]
public sealed partial class WorldMapContextWindow : BaseWindow
{
    public WorldMapContextWindow()
    {
        RobustXamlLoader.Load(this);
        SetState(WorldMapContextWindowState.World);
    }

    public void SetState(WorldMapContextWindowState state)
    {
        switch (state)
        {
            case WorldMapContextWindowState.World:
                AddMarkerButton.Visible = true;
                ChangeMarkerButton.Visible = false;
                DeleteMarkerButton.Visible = false;
                break;
            case WorldMapContextWindowState.Marker:
                AddMarkerButton.Visible = false;
                ChangeMarkerButton.Visible = true;
                DeleteMarkerButton.Visible = true;
                break;
        }
    }
}

public enum WorldMapContextWindowState : byte
{
    World = 0,
    Marker = 1,
}
