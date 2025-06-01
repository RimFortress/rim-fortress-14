using Content.Client._RF.UserInterface.Controls;
using Content.Client.Gameplay;
using Content.Client.UserInterface.Controls;
using Content.Client.UserInterface.Systems.Gameplay;
using Robust.Client.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client._RF.UserInterface;

public sealed class RimFortressState : GameplayStateBase
{
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
        base.Startup();

        UserInterfaceManager.LoadScreen<RimFortressScreen>();
        _loadController.LoadScreen();

        Screen.EnsureSetup();
    }

    protected override void Shutdown()
    {
        CommandBinds.Unregister<RimFortressState>();

        Screen.PopList.Clear();

        UserInterfaceManager.ClearWindows();
        _loadController.UnloadScreen();
        UserInterfaceManager.UnloadScreen();

        base.Shutdown();
    }

    protected override void OnKeyBindStateChanged(ViewportBoundKeyEventArgs args)
    {
        base.OnKeyBindStateChanged(args.Viewport == null
            ? new ViewportBoundKeyEventArgs(args.KeyEventArgs, Viewport.Viewport)
            : args);
    }
}
