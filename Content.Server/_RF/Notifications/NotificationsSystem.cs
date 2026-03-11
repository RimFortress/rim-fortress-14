using Content.Shared._RF.Notifications;
using Content.Shared._RF.Notifications.Components;

namespace Content.Server._RF.Notifications;

public sealed class NotificationsSystem : SharedNotificationsSystem
{
    public override bool RemoveNotification(Entity<NotificationComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.Remove(id))
            return false;

        Dirty(ent);
        return true;
    }

    public override void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        foreach (var id in ids)
        {
            ent.Comp.Notifications.Remove(id);
        }

        Dirty(ent);
    }
}
