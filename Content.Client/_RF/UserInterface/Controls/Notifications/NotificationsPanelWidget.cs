using Content.Client._RF.UserInterface.Controllers;
using Content.Shared._RF.Notifications;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._RF.UserInterface.Controls.Notifications;

public sealed partial class NotificationsPanelWidget : UIWidget
{
    private readonly NotificationsPanelUiController _panelController;
    private readonly Dictionary<Notification, NotificationButton> _notifications = new();
    private NotificationButton? _pressed;

    public NotificationsPanelWidget()
    {
        InitializeComponent();

        _panelController = UserInterfaceManager.GetUIController<NotificationsPanelUiController>();
    }

    private void OnSettingsPressed()
    {
        UserInterfaceManager
            .GetUIController<NotificationsSettingsUiController>()
            .ToggleWindow();
    }

    protected override void EnteredTree()
    {
        base.EnteredTree();

        _panelController.OnNotificationAdded += AddNotification;
        _panelController.OnNotificationRemoved += RemoveNotification;
    }

    protected override void ExitedTree()
    {
        base.ExitedTree();

        _panelController.OnNotificationAdded -= AddNotification;
        _panelController.OnNotificationRemoved -= RemoveNotification;
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
