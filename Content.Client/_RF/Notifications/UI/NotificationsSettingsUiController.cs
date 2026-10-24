using Robust.Client.UserInterface.Controls;
using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Controls;

namespace Content.Client._RF.Notifications.UI;

public sealed class NotificationsSettingsUiController : WindowUiController<NotificationsSettingsWindow>
{
    protected override NotificationsSettingsWindow EnsureWindow()
    {
        var window = base.EnsureWindow();
        window.EnsureSetup();
        LayoutContainer.SetAnchorPreset(window, LayoutContainer.LayoutPreset.Center);
        return window;
    }
}
