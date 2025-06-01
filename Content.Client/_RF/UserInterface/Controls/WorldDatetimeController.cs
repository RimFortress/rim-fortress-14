using Content.Client.GameTicking.Managers;
using Content.Shared._RF.GameTicking.Rules;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface.Controls;

public sealed class WorldDatetimeController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [UISystemDependency] private readonly MapSystem _map = default!;
    [UISystemDependency] private readonly ClientGameTicker _ticker = default!;

    private float _worldTemp = 293.15f;

    private RimFortressScreen Screen => (RimFortressScreen) _ui.ActiveScreen!;

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
            || !_map.TryGetMap(xform.MapID, out var map))
            return;

        if (_entityManager.TryGetComponent(map, out LightCycleComponent? cycle))
        {
            var time = _timing.CurTime
                .Add(cycle.Offset)
                .Subtract(_ticker.RoundStartTimeSpan);

            Screen.Datetime.UpdateInfo(time, cycle.Duration, _worldTemp);
        }
    }
}
