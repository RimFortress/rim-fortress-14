using Content.Shared._RF.Notifications.Components;
using Content.Shared.Humanoid;
using JetBrains.Annotations;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared._RF.Notifications.Systems;

/// <summary>
/// A system that provides an API for creating notifications for players about various events.
/// </summary>
public abstract class SharedNotificationsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Called every time a new notification is created. Use only on the client.
    /// </summary>
    public event Action<int>? OnNotificationAdded;

    /// <summary>
    /// Called every time a notification is deleted. Use only on the client.
    /// </summary>
    public event Action<int>? OnNotificationRemoved;

    private int _lastNotificationId;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NotificationComponent, ComponentHandleState>(OnHandleState);
        SubscribeLocalEvent<NotificationComponent, ComponentGetState>(OnGetState);

        SubscribeNetworkEvent<RemoveNotificationRequest>(OnRemoveNotificationRequest);
        SubscribeNetworkEvent<RemoveNotificationsRequest>(OnRemoveNotificationsRequest);
    }

    private void OnHandleState(Entity<NotificationComponent> ent, ref ComponentHandleState args)
    {
        if (args.Current is not NotificationComponentState state)
            return;

        foreach (var (id, _) in state.Notifications)
        {
            if (!ent.Comp.Notifications.ContainsKey(id))
                OnNotificationAdded?.Invoke(id);
        }

        foreach (var (id, _) in ent.Comp.Notifications)
        {
            if (!state.Notifications.ContainsKey(id))
                OnNotificationRemoved?.Invoke(id);
        }

        ent.Comp.Notifications = state.Notifications;
    }

    private void OnGetState(Entity<NotificationComponent> ent, ref ComponentGetState args)
    {
        args.State = new NotificationComponentState(ent.Comp.Notifications);
    }

    private void OnRemoveNotificationRequest(RemoveNotificationRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        RemoveNotification(uid, request.Id);
    }

    private void OnRemoveNotificationsRequest(RemoveNotificationsRequest request, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid)
            return;

        RemoveNotifications(uid, request.Ids);
    }

    /// <summary>
    /// Creates a notification for the player.
    /// </summary>
    /// <param name="ent">Entity of the player for whom the notification should be created.</param>
    /// <param name="protoId">Notification prototype.</param>
    [PublicAPI]
    public bool SendNotification(Entity<NotificationComponent?> ent, ProtoId<NotificationPrototype> protoId)
    {
        if (!Resolve(ent, ref ent.Comp) || !_proto.Resolve(protoId, out _))
            return false;

        return SendNotification(ent, new Notification(protoId));
    }

    /// <summary>
    /// Creates a notification for the player.
    /// </summary>
    /// <param name="ent">Entity of the player for whom the notification should be created.</param>
    /// <param name="protoId">Notification prototype.</param>
    /// <param name="target">The entity that triggered this notification.</param>
    [PublicAPI]
    public bool SendNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        EntityUid target)
    {
        if (!Resolve(ent, ref ent.Comp) || !_proto.Resolve(protoId, out var proto))
            return false;

        var notification = new Notification(
            protoId,
            target: GetNetEntity(target),
            expireAt: _timing.CurTime + proto.Duration);

        return SendNotification(ent, notification);
    }

    /// <summary>
    /// Creates a notification for the player.
    /// </summary>
    /// <param name="ent">Entity of the player for whom the notification should be created.</param>
    /// <param name="protoId">Notification prototype.</param>
    /// <param name="coords">Coordinates of the location that triggered this notification.</param>
    [PublicAPI]
    public bool SendNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        EntityCoordinates coords)
    {
        if (!Resolve(ent, ref ent.Comp) || !_proto.Resolve(protoId, out var proto))
            return false;

        var notification = new Notification(
            protoId,
            targetCoords: GetNetCoordinates(coords),
            expireAt: _timing.CurTime + proto.Duration);

        return SendNotification(ent, notification);
    }

    private bool SendNotification(Entity<NotificationComponent?> ent, Notification notification)
    {
        if (!Resolve(ent, ref ent.Comp) || !_proto.Resolve(notification.ProtoId, out var proto))
            return false;

        var same = ent.Comp.Notifications
            .FirstOrNull(x => x.Value.Equivalent(notification));

        if (same != null)
        {
            switch (proto.DuplicationPolicy)
            {
                case NotificationDuplicationPolicy.None:
                    return false;
                case NotificationDuplicationPolicy.Replace:
                    RemoveNotification(ent, same.Value.Key);
                    break;
                case NotificationDuplicationPolicy.Stack:
                    notification.Duplications = same.Value.Value.Duplications + 1;
                    RemoveNotification(ent, same.Value.Key);
                    break;
            }
        }

        _lastNotificationId++;
        ent.Comp.Notifications[_lastNotificationId] = notification;

        Dirty(ent);

        return true;
    }

    /// <summary>
    /// Returns the localized description of the notification.
    /// </summary>
    [PublicAPI, Pure]
    public string GetDescString(Entity<NotificationComponent?> ent, int id)
    {
        if (!Resolve(ent, ref ent.Comp) || !ent.Comp.Notifications.TryGetValue(id, out var notification))
            return string.Empty;

        return GetDescString(notification);
    }

    /// <summary>
    /// Returns the localized description of the notification.
    /// </summary>
    [PublicAPI, Pure]
    public string GetDescString(Notification notification)
    {
        if (!_proto.Resolve(notification.ProtoId, out var proto))
            return string.Empty;

        if (proto.TargetLocId != null
            && TryGetEntity(notification.Target, out var uid)
            && TryComp(uid, out HumanoidAppearanceComponent? appearance))
        {
            var entLoc = Loc.GetString(proto.EntityNameWrapper,
                ("name", MetaData(uid.Value).EntityName),
                ("sex", appearance.Sex.ToString().ToLowerInvariant()));
            return Loc.GetString(proto.DescId, (proto.TargetLocId, entLoc));
        }

        return Loc.GetString(proto.DescId);
    }

    /// <summary>
    /// Removes notification.
    /// </summary>
    /// <param name="ent">Player entity.</param>
    /// <param name="id">ID of the notification that needs to be deleted.</param>
    /// <returns>True, if the notification has been successfully deleted.</returns>
    [PublicAPI]
    public abstract bool RemoveNotification(Entity<NotificationComponent?> ent, int id);

    /// <summary>
    /// Removes multiple notifications.
    /// </summary>
    /// <param name="ent">Player entity.</param>
    /// <param name="ids">IDs of notifications that need to be deleted.</param>
    [PublicAPI]
    public abstract void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<NotificationComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            var toRemove = new List<int>();

            foreach (var (id, notification) in comp.Notifications)
            {
                if (notification.ExpireAt >= _timing.CurTime)
                    toRemove.Add(id);
            }

            RemoveNotifications(new(uid, comp), toRemove);
        }
    }
}

[Serializable, NetSerializable]
public sealed class RemoveNotificationRequest(int id) : EntityEventArgs
{
    [DataField]
    public int Id = id;
}

[Serializable, NetSerializable]
public sealed class RemoveNotificationsRequest(List<int> ids) : EntityEventArgs
{
    [DataField]
    public List<int> Ids = ids;
}
