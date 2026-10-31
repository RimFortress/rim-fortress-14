using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Climbs onto what can be climbed, such as a table. It is what the pathfinding asks of an agent that has a path through
/// it, see the goal of the rule.
/// </summary>
public sealed partial class Climb : BaseGoapAction<Climb>
{
    /// <summary>
    /// The entity to be climbed onto.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = "ClimbTarget";
}

public sealed partial class ClimbActionSystem : GoapActionSystem<Climb>
{
    [Dependency] private ClimbSystem _climb = null!;
    [Dependency] private EntityQuery<ClimbingComponent> _climbingQuery;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, Climb action) => 1f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, Climb action)
    {
        if (!_climbingQuery.TryComp(ent, out var climbing))
        {
            ComponentNotFound<ClimbingComponent>();
            return GoapActionResult.Failed;
        }

        // On top of it, the way goes on from there.
        if (climbing.IsClimbing)
            return GoapActionResult.Finished;

        // The climbing has begun.
        if (climbing.DoAfter != null)
            return GoapActionResult.Continuing;

        if (!TryGet(ent, action.TargetKey, out var target))
            return GoapActionResult.Failed;

        if (!TryComp(target, out ClimbableComponent? climbable))
        {
            CreateDump($"{ToPrettyString(target)} cannot be climbed onto");
            return GoapActionResult.Failed;
        }

        if (!_climb.CanVault(climbable, ent, target, out var reason))
        {
            CreateDump($"cannot climb onto {ToPrettyString(target)}: {reason}");
            return GoapActionResult.Failed;
        }

        return _climb.TryClimb(ent, ent, target, out _, climbable, climbing)
            ? GoapActionResult.Continuing
            : GoapActionResult.Failed;
    }
}
