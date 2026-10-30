using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Chooses a nearby coordinate and puts it into the resulting key.
/// </summary>
public sealed partial class PickAccessible : BaseGoapAction<PickAccessible>
{
    [DataField(required: true)]
    public StateKey<float> RangeKey;

    [DataField]
    public StateKey<EntityCoordinates> TargetCoordinates = "TargetCoordinates";

    /// <summary>
    /// What the place is searched for with: the algorithm of the query chooses it, and the rules of the profiles of the
    /// agent decide which places it can get to, so the same action does for the agents that can go by lava and for the
    /// ones that cannot.
    /// </summary>
    [DataField]
    public ProtoId<PathQueryPrototype> Query = "Wander";
}

public sealed partial class PickAccessibleSystem : GoapActionSystem<PickAccessible>
{
    [Dependency] private RfPathfindingSystem _pathfinding = null!;
    [Dependency] private EntityQuery<PathProfilesComponent> _profilesQuery;

    private const float DefaultRange = 7f;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, PickAccessible action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, PickAccessible action)
    {
        TryGet(ent, action.RangeKey, out var maxRange);

        if (maxRange == 0f)
            maxRange = DefaultRange;

        if (!_profilesQuery.TryComp(ent, out var profiles))
        {
            ComponentNotFound<PathProfilesComponent>();
            return false;
        }

        if (!_pathfinding.RequestPath((ent, profiles, ent.Comp), action.Query, null, maxRange))
        {
            CreateDump("none of the profiles of the agent can choose a place");
            return false;
        }

        CreateDump($"search for a place started. MaxRange: {maxRange}");
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, PickAccessible action)
    {
        if (!_profilesQuery.TryComp(ent, out var profiles))
            return GoapActionResult.Failed;

        // Waiting for the search to complete.
        if (!_pathfinding.TryGetResult((ent, profiles, ent.Comp), out var result, out _))
            return GoapActionResult.Continuing;

        _pathfinding.CancelPath((ent.Owner, profiles));

        if (result.Status == PathSearchStatus.NoPath || result.Path.Count == 0)
        {
            CreateDump("no place found");
            return GoapActionResult.Failed;
        }

        var node = result.Path[^1];
        Set(ent, action.TargetCoordinates, new EntityCoordinates(node.Graph, node.Box.Center));
        return GoapActionResult.Finished;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, PickAccessible action)
    {
        if (_profilesQuery.TryComp(ent, out var profiles))
            _pathfinding.CancelPath((ent.Owner, profiles));
    }
}
