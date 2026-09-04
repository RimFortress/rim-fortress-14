using Content.Server._RF.Notifications.Components;
using Content.Shared._RF.Notifications.Components;
using Content.Shared._RF.NPC.Systems;
using Content.Shared._RF.NPC.UtilityAi;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.Notifications.Systems;

/// <summary>
/// Manages <see cref="NotifyOnGoalGivenComponent"/>.
/// </summary>
public sealed partial class NotifyOnGoalGivenSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private OwnershipSystem _ownership = default!;
    [Dependency] private NotificationsSystem _notifications = default!;

    [SubscribeLocalEvent]
    private void OnNpcGoalGiven(Entity<NotifyOnGoalGivenComponent> ent, ref UtilityAiGoalGiven args)
    {
        if (!ent.Comp.Notifications.TryGetValue(args.Goal, out var protoId)
            || !_proto.Resolve(protoId, out var proto))
            return;

        var desc = Loc.GetString(proto.DescId, ("target", _notifications.GetEntityString(ent)));

        foreach (var owner in _ownership.GetOwners(ent))
        {
            if (HasComp<NotificationComponent>(owner))
                _notifications.SendNotification(owner, protoId, desc: desc);
        }
    }
}
