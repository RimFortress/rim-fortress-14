using Content.Shared._RF.Notifications;
using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.Notifications.Systems;
using Robust.Shared.Utility;

namespace Content.Client._RF.Notifications;

public sealed class NotificationsSystem : SharedNotificationsSystem
{
    public override bool RemoveNotification(Entity<NotificationComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.ContainsKey(id))
            return false;

        RaiseNetworkEvent(new RemoveNotificationRequest(id));
        return true;
    }

    public override void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids)
    {
        if (!Resolve(ent, ref ent.Comp) || ids.Count == 0)
            return;

        RaiseNetworkEvent(new RemoveNotificationsRequest(ids));
    }

    public override void FocusToNotification(Entity<NotificationComponent?> ent, Notification notification)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (notification.Target == null && notification.TargetCoords == null)
            return;

        if (!ent.Comp.Notifications.TryFirstOrNull(x => notification.Equivalent(x.Value), out var y))
            return;

        RaiseNetworkEvent(new FocusToNotificationRequest(y.Value.Key));
    }
}
