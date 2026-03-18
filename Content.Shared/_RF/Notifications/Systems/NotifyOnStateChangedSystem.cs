using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.NPC;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Notifications.Systems;

public sealed class NotifyOnStateChangedSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly OwnedSystem _owned = default!;
    [Dependency] private readonly SharedNotificationsSystem _notifications = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NotifyOnStateChangedComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<NotifyOnStateChangedComponent> ent, ref MobStateChangedEvent ev)
    {
        if (!ent.Comp.Notifications.TryGetValue(ev.NewMobState, out var protoId)
            || !_proto.Resolve(protoId, out var proto))
            return;

        var desc = Loc.GetString(proto.DescId, ("target", _notifications.GetEntityString(ent)));

        foreach (var uid in _owned.GetOwners(ent))
        {
            if (!TryComp(uid, out NotificationComponent? comp))
                continue;

            _notifications.SendNotification(new(uid, comp), protoId, ent, desc);

            if (ent.Comp.RemoveOthers && ent.Comp.Notifications.TryGetValue(ev.OldMobState, out protoId))
                _notifications.RemoveNotification(new(uid, comp), protoId);
        }
    }
}
