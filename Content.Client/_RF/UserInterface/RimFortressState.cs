using Content.Client._RF.UserInterface.Controls;
using Content.Client.Gameplay;
using Content.Client.GameTicking.Managers;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Shared.Light.Components;
using Robust.Client.GameObjects;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client._RF.UserInterface;

public sealed class RimFortressState : GameplayStateBase
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private MetaDataSystem _meta = default!;
    private MapSystem _map = default!;
    private ClientGameTicker _ticker = default!;

    private bool _setup;

    private RimFortressScreen Screen => (RimFortressScreen) UserInterfaceManager.ActiveScreen!;
    private MainViewport Viewport => UserInterfaceManager.ActiveScreen!.GetWidget<MainViewport>()!;

    private readonly GameplayStateLoadController _loadController;

    public RimFortressState()
    {
        IoCManager.InjectDependencies(this);

        _loadController = UserInterfaceManager.GetUIController<GameplayStateLoadController>();
    }

    protected override void Startup()
    {
        EnsureSetup();
        base.Startup();

        UserInterfaceManager.LoadScreen<RimFortressScreen>();
        _loadController.LoadScreen();
    }

    protected override void Shutdown()
    {
        CommandBinds.Unregister<RimFortressState>();

        UserInterfaceManager.ClearWindows();
        _loadController.UnloadScreen();
        UserInterfaceManager.UnloadScreen();

        base.Shutdown();
    }

    private void EnsureSetup()
    {
        if (_setup)
            return;

        _setup = true;
        _meta = _entityManager.System<MetaDataSystem>();
        _map = _entityManager.System<MapSystem>();
        _ticker = _entityManager.System<ClientGameTicker>();
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
        if (args.Viewport == null)
            base.OnKeyBindStateChanged(new ViewportBoundKeyEventArgs(args.KeyEventArgs, Viewport.Viewport));
        else
            base.OnKeyBindStateChanged(args);
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        base.FrameUpdate(e);

        if (_player.LocalEntity is not { } entity
            || !_entityManager.TryGetComponent(entity, out TransformComponent? xform))
            return;

        var map = _map.GetMap(xform.MapID);
        var pausedTime = _meta.GetPauseTime(map);

        if (_entityManager.TryGetComponent(map, out LightCycleComponent? cycle))
        {
            var time = _timing.CurTime
                .Add(cycle.Offset)
                .Subtract(_ticker.RoundStartTimeSpan)
                .Subtract(pausedTime);

            Screen.Datetime.UpdateInfo(time, cycle.Duration); // TODO: dynamic world map temperature
        }
    }
}
