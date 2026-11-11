using System.Linq;
using Content.Shared._RF.NPC.Components;
using Content.Shared._RF.NPC.Engagement;
using Content.Shared._RF.NPC.Engagement.Components;
using Content.Shared._RF.NPC.Groups;
using Content.Shared._RF.NPC.Groups.Components;
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
        => TriggerOwners(ent, args.Actor, $"{Prototype(ent)?.ID}.joined.{args.Role}");

    // Whoever leaves a group that is being taken apart (moved to another, made again) is not a loss for the owner.
    [SubscribeLocalEvent]
    private void OnRoleLeft(Entity<EngagementComponent> ent, ref EngagementRoleLeft args)
    {
        if (!HasComp<GroupDissolvingComponent>(ent))
            TriggerOwners(ent, args.Actor, $"{Prototype(ent)?.ID}.left.{args.Role}");
    }

    [SubscribeLocalEvent]
    private void OnGroupFormed(Entity<EngagementComponent> ent, ref GroupFormed args)
        => TriggerOwners(ent, null, $"{Prototype(ent)?.ID}.formed");

    [SubscribeLocalEvent]
    private void OnGroupMerged(Entity<EngagementComponent> ent, ref GroupMerged args)
        => TriggerOwners(ent, null, $"{Prototype(ent)?.ID}.merged");

    private void TriggerOwners(EntityUid engagement, EntityUid? actor, string key)
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
