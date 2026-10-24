using System.Numerics;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.World.UI;

public sealed partial class WorldMapContextWindow : RfWindow
{
    public WorldMapContextWindow()
    {
        InitializeComponent();
        SetState(WorldMapContextWindowState.World);
    }

    // It is a menu, not a window to move.
    protected override DragMode GetDragModeFor(Vector2 relativeMousePos) => DragMode.None;

    public void SetState(WorldMapContextWindowState state)
    {
        AddMarkerButton.Visible = state == WorldMapContextWindowState.World;
        ChangeMarkerButton.Visible = state == WorldMapContextWindowState.Marker;
        DeleteMarkerButton.Visible = state == WorldMapContextWindowState.Marker;
        TpHereButton.Visible = state is WorldMapContextWindowState.Marker or WorldMapContextWindowState.NoChangeMarker;
    }
}

public enum WorldMapContextWindowState : byte
{
    World = 0,
    Marker = 1,
    NoChangeMarker = 2,
}
