using Content.Client._RF.Notifications;
using Content.Shared._RF.Notifications;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;

namespace Content.Client._RF.UserInterface.Controllers;

public sealed class NotificationsPanelUiController : UIController, IOnSystemLoaded<NotificationsSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;

    /// <summary>
    /// Called every time a new notification is created.
    /// </summary>
    public event Action<Notification>? OnNotificationAdded;

    /// <summary>
    /// Called every time a notification is deleted.
    /// </summary>
    public event Action<Notification>? OnNotificationRemoved;

    public void OnSystemLoaded(NotificationsSystem system)
    {
        system.OnNotificationAdded += args =>
        {
            if (_player.LocalEntity is { } uid && !system.Ignored(uid, args.ProtoId))
                OnNotificationAdded?.Invoke(args);
        };

        system.OnNotificationRemoved += args =>
        {
            if (_player.LocalEntity is { } uid && !system.Ignored(uid, args.ProtoId))
                OnNotificationRemoved?.Invoke(args);
        };
    }
}
