using Content.Client._RF.UserInterface.Controls.Notifications;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._RF.UserInterface.Controllers;

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
