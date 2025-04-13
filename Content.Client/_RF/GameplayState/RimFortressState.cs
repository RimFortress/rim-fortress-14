using Content.Client._RF.World;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input.Binding;
using Robust.Shared.Timing;

namespace Content.Client._RF.GameplayState;

public sealed class RimFortressState : GameplayStateBase
{
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private RimFortressWorldSystem _world = default!;

    private bool _setup;

    private RimFortressScreen Screen => (RimFortressScreen) UserInterfaceManager.ActiveScreen!;
    private MainViewport Viewport => UserInterfaceManager.ActiveScreen!.GetWidget<MainViewport>()!;

    private readonly GameplayStateLoadController _loadController;

    private List<EntityUid> _lastPops = new();

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

        _lastPops.Clear();

        base.Shutdown();
    }

    private void EnsureSetup()
    {
        if (_setup)
            return;

        _setup = true;
        _world = _entityManager.System<RimFortressWorldSystem>();
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
            || _world.GetPLayerPops(entity) is not { } pops
            || _lastPops == pops)
            return;

        Screen.PopList.SetPops(pops);
        _lastPops = pops;
    }
}
