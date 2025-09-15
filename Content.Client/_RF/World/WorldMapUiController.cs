using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.World.UI;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.World;

public sealed class WorldMapUiController : WindowUiController<WorldMapWindow>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly TransformSystem _transform = default!;

    protected override WorldMapWindow EnsureWindow()
    {
        Window = base.EnsureWindow();

        if (_player.LocalEntity is { Valid: true } player)
        {
            Window.Set(_transform.GetMap(player));
            Window.WorldMap.CenterToCoordinates(_transform.GetMapCoordinates(player).Position);
        }

        LayoutContainer.SetAnchorPreset(Window, LayoutContainer.LayoutPreset.TopLeft);
        return Window;
    }
}
