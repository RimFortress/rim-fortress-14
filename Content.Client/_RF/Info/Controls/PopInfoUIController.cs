using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.Info.Controls;

public sealed class PopInfoUIController : UIController
{
    private PopInfoWindow? _window;

    public void OpenWindow(EntityUid uid)
    {
        EnsureWindow();
        _window!.SetInfo(uid);
        _window.Open();
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<PopInfoWindow>();
        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
    }
}

