using Content.Client._RF.World.UI;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.World;

public sealed class WorldMapUiController : UIController
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly TransformSystem _transform = default!;

    private WorldMapWindow? _window;

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
            _window.Close();
        else
            _window.Open();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<WorldMapWindow>();

        if (_player.LocalEntity is { Valid: true } player)
        {
            _window.Set(_transform.GetMap(player));
            _window.WorldMap.CenterToCoordinates(_transform.GetMapCoordinates(player).Position);
        }

        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.CenterLeft);
    }
}
