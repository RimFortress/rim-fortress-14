using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.Notifications.Systems;

namespace Content.Client._RF.Notifications;

public sealed class NotificationsSystem : SharedNotificationsSystem
{
    public override bool RemoveNotification(Entity<NotificationComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.ContainsKey(id))
            return false;

        RaiseNetworkEvent(new RemoveNotificationRequest(id), ent);
        return true;
    }

    public override void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        RaiseNetworkEvent(new RemoveNotificationsRequest(ids), ent);
    }
}
