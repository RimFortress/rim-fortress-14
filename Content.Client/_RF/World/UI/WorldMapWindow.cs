using Content.Client._RF.UserInterface.Controls;
using Content.Shared._RF.World;
using Content.Shared._RF.World.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Map.Components;

namespace Content.Client._RF.World.UI;

public sealed partial class WorldMapWindow : RfWindow
{
    [Dependency] private IEntityManager _entity = default!;
    [Dependency] private IPlayerManager _player = default!;

    public WorldMapWindow()
    {
        IoCManager.InjectDependencies(this);
        InitializeComponent();

        var world = _entity.System<SharedRimFortressWorldSystem>();
        var map = _entity.System<MapSystem>();

        ResetCoordsButton.OnPressed += _ =>
        {
            if (WorldMap.MapUid is { } mapUid
                && _entity.TryGetComponent(mapUid, out MapGridComponent? grid)
                && _entity.TryGetComponent(_player.LocalEntity, out TransformComponent? xformForm))
                WorldMap.CenterToCoordinates(map.GetTileRef(mapUid, grid, xformForm.Coordinates).GridIndices);
        };

        FactionColor.OnColorChanged += color =>
        {
            if (_player.LocalEntity is { } uid)
                world.SetPlayerFactionColor(uid, color);
        };

        // The map is a control of the old kind, the space of the box is its size.
        MapArea.OnResized += () => WorldMap.SetSize = MapArea.Size;

        Scope.EveryFrame(_ => Coordinates.Text = $"{(Vector2i) WorldMap.MousePos()}");
    }

    public void Set(EntityUid? mapUid)
    {
        WorldMap.MapUid = mapUid;

        if (_entity.TryGetComponent(_player.LocalEntity, out RimFortressPlayerComponent? player))
            FactionColor.Color = player.FactionColor;
    }
}
