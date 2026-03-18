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
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly LocId EntityNameWrapper = "notification-entity-name-wrapper";

    /// <summary>
    /// Called every time a new notification is created. Use only on the client.
    /// </summary>
    public event Action<Notification>? OnNotificationAdded;

    /// <summary>
    /// Called every time a notification is deleted. Use only on the client.
    /// </summary>
    public event Action<Notification>? OnNotificationRemoved;

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

        foreach (var (id, notification) in ent.Comp.Notifications)
        {
            if (!state.Notifications.ContainsKey(id))
                OnNotificationRemoved?.Invoke(notification);
        }

        foreach (var (id, notification) in state.Notifications)
        {
            if (!ent.Comp.Notifications.ContainsKey(id))
                OnNotificationAdded?.Invoke(notification);
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
    /// <param name="desc">Description override for notification.</param>
    [PublicAPI]
    public bool SendNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        string? desc = null)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return false;

        var notification = new Notification(
            protoId,
            desc ?? Loc.GetString(proto.DescId),
            _timing.CurTime,
            expireAt: _timing.CurTime + proto.Duration);

        return SendNotification(ent, notification);
    }

    /// <summary>
    /// Creates a notification for the player.
    /// </summary>
    /// <param name="ent">Entity of the player for whom the notification should be created.</param>
    /// <param name="protoId">Notification prototype.</param>
    /// <param name="target">The entity that triggered this notification.</param>
    /// <param name="desc">Description override for notification.</param>
    [PublicAPI]
    public bool SendNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        EntityUid target,
        string? desc = null)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return false;

        var notification = new Notification(
            protoId,
            desc ?? Loc.GetString(proto.DescId),
            _timing.CurTime,
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
    /// <param name="desc">Description override for notification.</param>
    [PublicAPI]
    public bool SendNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        EntityCoordinates coords,
        string? desc = null)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(protoId, out var proto))
            return false;

        var notification = new Notification(
            protoId,
            desc ?? Loc.GetString(proto.DescId),
            _timing.CurTime,
            targetCoords: GetNetCoordinates(coords),
            expireAt: _timing.CurTime + proto.Duration);

        return SendNotification(ent, notification);
    }

    private bool SendNotification(Entity<NotificationComponent?> ent, Notification notification)
    {
        if (!Resolve(ent, ref ent.Comp) || !Proto.Resolve(notification.ProtoId, out var proto))
            return false;

        var same = ent.Comp.Notifications
            .FirstOrNull(x => x.Value.Equivalent(notification));

        if (same != null)
        {
            if (proto.ReplaceDuplicate)
                RemoveNotification(ent, same.Value.Key, false);
            else
                return false;
        }

        _lastNotificationId++;
        ent.Comp.Notifications[_lastNotificationId] = notification;

        Dirty(ent);

        return true;
    }

    [PublicAPI, Pure]
    public string GetEntityString(EntityUid uid)
    {
        var name = MetaData(uid).EntityName;

        if (!TryComp(uid, out HumanoidAppearanceComponent? appearance))
            return name;

        return Loc.GetString(EntityNameWrapper,
            ("name", name),
            ("sex", appearance.Sex.ToString().ToLowerInvariant()));
    }

    /// <summary>
    /// Removes notification.
    /// </summary>
    /// <param name="ent">Player entity.</param>
    /// <param name="protoId">Prototype of the notification that needs to be deleted.</param>
    /// <param name="dirty"></param>
    /// <returns>True, if the notification has been successfully deleted.</returns>
    [PublicAPI]
    public bool RemoveNotification(
        Entity<NotificationComponent?> ent,
        ProtoId<NotificationPrototype> protoId,
        bool dirty = true)
    {
        if (!Resolve(ent, ref ent.Comp))
            return false;

        foreach (var (id, notification) in ent.Comp.Notifications)
        {
            if (notification.ProtoId == protoId)
                return RemoveNotification(ent, id, dirty);
        }

        return false;
    }

    /// <summary>
    /// Removes notification.
    /// </summary>
    /// <param name="ent">Player entity.</param>
    /// <param name="id">ID of the notification that needs to be deleted.</param>
    /// <param name="dirty"></param>
    /// <returns>True, if the notification has been successfully deleted.</returns>
    [PublicAPI]
    public abstract bool RemoveNotification(Entity<NotificationComponent?> ent, int id, bool dirty = true);

    /// <summary>
    /// Removes multiple notifications.
    /// </summary>
    /// <param name="ent">Player entity.</param>
    /// <param name="ids">IDs of notifications that need to be deleted.</param>
    [PublicAPI]
    public abstract void RemoveNotifications(Entity<NotificationComponent?> ent, List<int> ids);

    /// <summary>
    /// Teleports the entity to the coordinates that triggered the notification.
    /// </summary>
    [PublicAPI]
    public void FocusToNotification(Entity<NotificationComponent?> ent, int id)
    {
        if (Resolve(ent, ref ent.Comp) && ent.Comp.Notifications.TryGetValue(id, out var notification))
            FocusToNotification(ent, notification);
    }

    /// <summary>
    /// Teleports the entity to the coordinates that triggered the notification.
    /// </summary>
    [PublicAPI]
    public abstract void FocusToNotification(Entity<NotificationComponent?> ent, Notification notification);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var enumerator = EntityQueryEnumerator<NotificationComponent>();
        while (enumerator.MoveNext(out var uid, out var comp))
        {
            var toRemove = new List<int>();

            foreach (var (id, notification) in comp.Notifications)
            {
                if (notification.ExpireAt < _timing.CurTime)
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

[Serializable, NetSerializable]
public sealed class FocusToNotificationRequest(int id) : EntityEventArgs
{
    [DataField]
    public int Id = id;
}
