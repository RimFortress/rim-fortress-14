using Content.Server._RF.NPC.Components;
using Content.Server._RF.NPC.Systems;
using Content.Shared._RF.Notifications;
using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.Notifications.Systems;
using Content.Shared.Follower;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Systems;

public sealed class NotificationsSystem : SharedNotificationsSystem
{
    [Dependency] private readonly FollowerSystem _follower = default!;
    [Dependency] private readonly TransformSystem _xform = default!;

    private static readonly ProtoId<NotificationPrototype> TaskSuspendedNotify = "TaskSuspended";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ControllableNpcComponent, NpcTaskFinished>(OnTaskFinished);
        SubscribeNetworkEvent<FocusToNotificationRequest>(OnFocusToNotificationRequest);
    }

    private void OnTaskFinished(Entity<ControllableNpcComponent> ent, ref NpcTaskFinished args)
    {
        if (args.Status != TaskFinishStatus.Failed || !Proto.Resolve(args.Task, out var proto))
            return;

        var desc = Loc.GetString(Proto.Index(TaskSuspendedNotify).DescId,
            ("target", GetEntityString(ent)),
            ("task", proto.Name),
            ("taskColor", proto.OverlayColor.ToHex()),
            ("hasReason", args.Reason != null),
            ("reason", args.Reason ?? string.Empty));

        foreach (var uid in ent.Comp.CanControl)
        {
            if (args.Target is { } target)
                SendNotification(uid, TaskSuspendedNotify, target, desc);
            else
                SendNotification(uid, TaskSuspendedNotify, desc);
        }
    }

    private void OnFocusToNotificationRequest(FocusToNotificationRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is { } uid)
            FocusToNotification(uid, request.Id);
    }

    public override bool RemoveNotification(Entity<NotificationComponent?> ent, int id, bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.Remove(id))
            return false;

        if (dirty)
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
