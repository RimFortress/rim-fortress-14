using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.NPC;
using Content.Shared.Mobs;

namespace Content.Shared._RF.Notifications.Systems;

public sealed class NotifyOnStateChangedSystem : EntitySystem
{
    [Dependency] private readonly OwnedSystem _owned = default!;
    [Dependency] private readonly SharedNotificationsSystem _notifications = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<NotifyOnStateChangedComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(Entity<NotifyOnStateChangedComponent> ent, ref MobStateChangedEvent ev)
    {
        if (!ent.Comp.Notifications.TryGetValue(ev.NewMobState, out var proto))
            return;

        foreach (var uid in _owned.GetOwners(ent))
        {
            if (TryComp(uid, out NotificationComponent? comp))
                _notifications.SendNotification(new(uid, comp), proto, ent);
        }
    }
}
