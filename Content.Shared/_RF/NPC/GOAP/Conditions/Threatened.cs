using System.Numerics;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding.Systems;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether there is an enemy of the agent near the target entity that can shoot it: an enemy is a living entity of a
/// faction that is hostile to the agent.
/// </summary>
public sealed partial class Threatened : BaseGoapCondition<Threatened>
{
    /// <summary>
    /// The entity that is looked at: the agent itself if it is not set.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    /// <summary>
    /// How far from the target an enemy is looked for.
    /// </summary>
    [DataField]
    public float Range = 20f;

    /// <summary>
    /// If true, an enemy that has a wall (or anything that stops projectiles) between it and the target does not count.
    /// </summary>
    [DataField]
    public bool Sight = true;
}

public sealed partial class ThreatenedConditionSystem : GoapConditionSystem<Threatened>
{
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private SharedRfPathfindingSystem _pathfinding = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, Threatened condition)
    {
        if (!TryGet(state, condition.TargetKey, out var target))
            return false;

        var coords = _transform.GetMapCoordinates(target);
        var graph = Transform(target).GridUid;

        foreach (var other in _lookup.GetEntitiesInRange<NpcFactionMemberComponent>(coords, condition.Range))
        {
            if (other.Owner == uid || other.Owner == target || !_mobState.IsAlive(other.Owner))
                continue;

            if (!IsHostile(uid, other.Comp))
                continue;

            if (condition.Sight && graph is { } grid && IsSightBlocked(grid, target, other.Owner))
                continue;

            CreateDump($"{ToPrettyString(other.Owner)} threatens {ToPrettyString(target)}");
            return true;
        }

        return false;
    }

    private bool IsHostile(EntityUid agent, NpcFactionMemberComponent member)
    {
        foreach (var faction in member.Factions)
        {
            if (_faction.IsFactionHostile(faction, agent))
                return true;
        }

        return false;
    }

    private bool IsSightBlocked(EntityUid graph, EntityUid from, EntityUid to)
    {
        var inverse = _transform.GetInvWorldMatrix(graph);
        var a = Vector2.Transform(_transform.GetWorldPosition(from), inverse);
        var b = Vector2.Transform(_transform.GetWorldPosition(to), inverse);

        return _pathfinding.IsSightBlocked(graph, a, b);
    }
}
