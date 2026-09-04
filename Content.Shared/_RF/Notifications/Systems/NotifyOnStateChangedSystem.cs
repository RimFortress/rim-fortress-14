using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared.Mobs;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.Notifications.Systems;

public sealed partial class NotifyOnStateChangedSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private SharedNotificationsSystem _notifications = default!;

    [SubscribeLocalEvent]
    private void OnMobStateChanged(Entity<NotifyOnStateChangedComponent> ent, ref MobStateChangedEvent ev)
    {
        if (!ent.Comp.Notifications.TryGetValue(ev.NewMobState, out var protoId)
            || !_proto.Resolve(protoId, out var proto))
            return;

        var desc = Loc.GetString(proto.DescId, ("target", _notifications.GetEntityString(ent)));

        foreach (var uid in _ownership.GetOwners(ent))
        {
            if (!TryComp(uid, out NotificationComponent? comp))
                continue;

            _notifications.SendNotification(new(uid, comp), protoId, ent, desc);

            if (ent.Comp.RemoveOthers && ent.Comp.Notifications.TryGetValue(ev.OldMobState, out protoId))
                _notifications.RemoveNotification(new(uid, comp), protoId);
        }
    }
}
