using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controls.NpcJobs;

public sealed class NpcJobsPriorityUiController : UIController
{
    private NpcJobsPriorityWindow? _window;

    public void ToggleWindow()
    {
        EnsureWindow();

        if (_window!.IsOpen)
            _window.Close();
        else
        {
            _window.Open();
            _window.BuildTable();
        }
    }

    private void EnsureWindow()
    {
        if (_window is { Disposed: false })
            return;

        _window = UIManager.CreateWindow<NpcJobsPriorityWindow>();
        _window.EnsureSetup();

        LayoutContainer.SetAnchorPreset(_window, LayoutContainer.LayoutPreset.Center);
    }
}
