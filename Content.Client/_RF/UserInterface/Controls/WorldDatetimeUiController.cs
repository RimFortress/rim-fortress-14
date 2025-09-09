using Content.Client.GameTicking.Managers;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

public sealed class WorldDatetimeUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [UISystemDependency] private readonly MapSystem _map = default!;
    [UISystemDependency] private readonly ClientGameTicker _ticker = default!;

    private float _worldTemp = 293.15f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<WorldTemperatureChangedMessage>(OnTemperatureChanged);
    }

    public void OnTemperatureChanged(WorldTemperatureChangedMessage msg, EntitySessionEventArgs args)
    {
        if (!_entityManager.TryGetComponent(_player.LocalEntity, out TransformComponent? xform)
            || !_map.TryGetMap(xform.MapID, out var map)
            || _entityManager.GetEntity(msg.WorldEntity) != map)
            return;

        _worldTemp = msg.Temperature;
    }

    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_entityManager.TryGetComponent(_player.LocalEntity, out TransformComponent? xform)
            || !_map.TryGetMap(xform.MapID, out var map)
            || !_entityManager.TryGetComponent(map, out LightCycleComponent? cycle))
            return;

        var time = _timing.CurTime
            .Add(cycle.Offset)
            .Subtract(_ticker.RoundStartTimeSpan);

        UIManager
            .GetActiveUIWidgetOrNull<WorldDatetimeWidget>()
            ?.UpdateInfo(time, cycle.Duration, _worldTemp);
    }
}
