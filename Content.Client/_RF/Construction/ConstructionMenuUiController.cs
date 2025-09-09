using Content.Client._RF.Construction.Controls;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;

namespace Content.Client._RF.Construction;

public sealed class ConstructionMenuUiController : UIController,
    IOnSystemLoaded<InputSystem>, IOnSystemUnloaded<InputSystem>
{
    private RfConstructionMenu? _window;

    public void OnSystemLoaded(InputSystem system)
    {
        CommandBinds.Builder
            .Bind(EngineKeyFunctions.EditorCancelPlace, new PointerInputCmdHandler(OnCancelPlace, outsidePrediction: true))
            .Register<ConstructionMenuUiController>();
    }

    public void OnSystemUnloaded(InputSystem system)
    {
        CommandBinds.Unregister<ConstructionMenuUiController>();
    }

    private bool OnCancelPlace(in PointerInputCmdHandler.PointerInputCmdArgs args)
    {
        if (_window == null)
            return false;

        _window.EraseButton.Pressed = false;
        _window.Metadata.Visible = false;
        return false;
    }

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

        _window = UIManager.CreateWindow<RfConstructionMenu>();
        _window.EnsureSetup();

        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
    }
}
