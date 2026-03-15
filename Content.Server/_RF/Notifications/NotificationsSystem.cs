using Content.Shared._RF.Notifications;
using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.Notifications.Systems;
using Content.Shared.Follower;
using Robust.Server.GameObjects;

namespace Content.Server._RF.Notifications;

public sealed class NotificationsSystem : SharedNotificationsSystem
{
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<FocusToNotificationRequest>(OnFocusToNotificationRequest);
    }

    private void OnFocusToNotificationRequest(FocusToNotificationRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } uid)
            FocusToNotification(uid, request.Id);
    }

    public override bool RemoveNotification(Entity<NotificationComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.Remove(id))
            return false;

        Dirty(ent);
        return true;
    }

    public override void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids)
    {
        if (!Resolve(ent, ref ent.Comp) || ids.Count == 0)
            return;

        foreach (var id in ids)
        {
            ent.Comp.Notifications.Remove(id);
        }

        Dirty(ent);
    }

    public override void FocusToNotification(Entity<NotificationComponent?> ent, Notification notification)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (TryGetEntity(notification.Target, out var uid))
            _follower.StartFollowingEntity(ent, uid.Value);
        else if (GetCoordinates(notification.TargetCoords) is { } coords)
            _xform.SetCoordinates(ent, coords);
    }
}
