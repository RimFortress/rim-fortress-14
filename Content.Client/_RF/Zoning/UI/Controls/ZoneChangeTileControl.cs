using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.Zoning.Components;
using JetBrains.Annotations;

namespace Content.Client._RF.Zoning.UI.Controls;

public sealed partial class ZoneChangeTileControl : RfBoxContainer
{
    private readonly ZoningUiController _controller;
    private Entity<ZoneComponent>? _zone;

    public ZoneChangeTileControl()
    {
        InitializeComponent();

        _controller = UserInterfaceManager.GetUIController<ZoningUiController>();
    }

    private void OnExpandPressed()
    {
        if (_zone != null)
            _controller.AddTileSelection(_zone.Value);
    }

    private void OnShrinkPressed()
    {
        if (_zone != null)
            _controller.RemoveTileSelection(_zone.Value);
    }

    private void OnDeletePressed()
    {
        if (_zone != null)
            _controller.DeleteZone(_zone.Value);
    }

    [PublicAPI]
    public void SetZone(Entity<ZoneComponent>? zone)
    {
        _zone = zone;
    }
}
