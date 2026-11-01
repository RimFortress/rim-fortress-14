using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.Trigger.Components.Triggers;
using Content.Shared.Trigger.Systems;

namespace Content.Shared._RF.Trigger.Systems;

public sealed partial class TriggerOnEngagementSystem : EntitySystem
{
    [Dependency] private TriggerSystem _trigger = null!;

    [Dependency] private EntityQuery<OwnershipComponent> _ownershipQuery;
    [Dependency] private EntityQuery<TriggerOnEngagementComponent> _triggerQuery;

    [SubscribeLocalEvent]
    private void OnRoleJoined(Entity<EngagementComponent> ent, ref EngagementRoleJoined args)
        => TriggerOwners(ent, args.Actor, $"{ent.Comp.Kind}.joined.{args.Role}");

    [SubscribeLocalEvent]
    private void OnRoleLeft(Entity<EngagementComponent> ent, ref EngagementRoleLeft args)
        => TriggerOwners(ent, args.Actor, $"{ent.Comp.Kind}.left.{args.Role}");

    private void TriggerOwners(EntityUid engagement, EntityUid actor, string key)
    {
        if (!_ownershipQuery.TryComp(engagement, out var ownership))
            return;

        foreach (var owner in ownership.Owners.ToArray())
        {
            if (_triggerQuery.HasComp(owner))
                _trigger.Trigger(owner, actor, key, predicted: false);
        }
    }
}
