using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Climbing.Components;
using Content.Shared.Climbing.Systems;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions.Interaction;

/// <summary>
/// Climbs onto what can be climbed that is at the coordinates, such as a table. It is what the pathfinding asks of an
/// agent that has a path through it, see the goal of the rule.
/// </summary>
public sealed partial class Climb : BaseGoapAction<Climb>
{
    /// <summary>
    /// The coordinates of what is to be climbed onto.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "ClimbTarget";
}

public sealed partial class ClimbActionSystem : GoapActionSystem<Climb>
{
    [Dependency] private ClimbSystem _climb = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private EntityQuery<ClimbingComponent> _climbingQuery;

    // How far from the coordinates what is climbed onto can be, it is in the middle of the tile.
    private const float Range = 0.45f;

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

        if (!TryGet(ent, action.TargetKey, out var coordinates))
            return GoapActionResult.Failed;

        Entity<ClimbableComponent>? target = null;
        var nearest = float.MaxValue;

        foreach (var climbable in _lookup.GetEntitiesInRange<ClimbableComponent>(coordinates, Range))
        {
            var distance = (_transform.GetWorldPosition(climbable) - _transform.ToMapCoordinates(coordinates).Position).Length();

            if (distance >= nearest)
                continue;

            nearest = distance;
            target = climbable;
        }

        if (target is not { } found)
        {
            CreateDump($"nothing to climb onto at {coordinates}");
            return GoapActionResult.Failed;
        }

        if (!_climb.CanVault(found.Comp, ent, found.Owner, out var reason))
        {
            CreateDump($"cannot climb onto {ToPrettyString(found)}: {reason}");
            return GoapActionResult.Failed;
        }

        return _climb.TryClimb(ent, ent, found.Owner, out _, found.Comp, climbing)
            ? GoapActionResult.Continuing
            : GoapActionResult.Failed;
    }
}
