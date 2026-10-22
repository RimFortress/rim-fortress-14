using Content.Client._RF.UserInterface.Controllers;
using Content.Client._RF.UserInterface.Scope;
using Content.Shared._RF.Notifications;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.Notifications;

public sealed partial class NotificationsPanelWidget : RfWidget
{
    private readonly Dictionary<Notification, NotificationButton> _notifications = new();
    private NotificationButton? _pressed;

    public NotificationsPanelWidget()
    {
        InitializeComponent();

        Settings.OnPressed += _ => Get<NotificationsSettingsUiController>().ToggleWindow();

        Scope.ListenController<NotificationsPanelUiController, Notification>(
            (c, h) => c.OnNotificationAdded += h,
            (c, h) => c.OnNotificationAdded -= h,
            AddNotification);
        Scope.ListenController<NotificationsPanelUiController, Notification>(
            (c, h) => c.OnNotificationRemoved += h,
            (c, h) => c.OnNotificationRemoved -= h,
            RemoveNotification);
    }

    private void AddNotification(Notification notification)
    {
        var button = new NotificationButton(notification);

        button.OnPressed += _ =>
        {
            if (_pressed != null)
                _pressed.Pressed = false;

            _pressed = button.Pressed ? button : null;
        };

        button.EnsureSetup();

        _notifications[notification] = button;
        Notifications.AddChild(button);

        Header.Visible = Notifications.ChildCount > 0;
        Scroll.Visible = Notifications.ChildCount > 0;
    }

    private void RemoveNotification(Notification notification)
    {
        if (_notifications.FirstOrNull(x => x.Key.Id == notification.Id)?.Value is not { } button)
            return;

        Notifications.RemoveChild(button);

        Header.Visible = Notifications.ChildCount > 0;
        Scroll.Visible = Notifications.ChildCount > 0;
    }
}
