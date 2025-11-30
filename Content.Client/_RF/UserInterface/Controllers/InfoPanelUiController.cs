using Content.Client._RF.World;
using Content.Shared._RF.GameTicking.Rules;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed class InfoPanelUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [UISystemDependency] private readonly MapSystem _map = default!;
    [UISystemDependency] private readonly RimFortressWorldSystem _world = default!;

    public float WorldTemp { get; private set; } = 293.15f;

    public event Action? OnWorldTempChanged;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WorldTemperatureChangedMessage>(OnTemperatureChanged);
    }

    private void OnTemperatureChanged(WorldTemperatureChangedMessage msg, EntitySessionEventArgs args)
    {
        if (!_entityManager.TryGetComponent(_player.LocalEntity, out TransformComponent? xform)
            || !_map.TryGetMap(xform.MapID, out var map)
            || _entityManager.GetEntity(msg.WorldEntity) != map)
            return;

        WorldTemp = msg.Temperature;
        OnWorldTempChanged?.Invoke();
    }

    public TimeSpan GetWorldTime() => _world.WorldDateTime();
}
