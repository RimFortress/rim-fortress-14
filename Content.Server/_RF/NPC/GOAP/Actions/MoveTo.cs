using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.GOAP.Actions;

public sealed partial class MoveTo : BaseGoapAction<MoveTo>
{
    /// <summary>
    /// Should we assume the MovementTarget is reachable during planning or should we pathfind to it?
    /// </summary>
    [DataField]
    public bool PathfindInPlanning = true;

    /// <summary>
    /// When we're finished moving to the target should we remove its key?
    /// </summary>
    [DataField]
    public bool RemoveKeyOnFinish = true;

    /// <summary>
    /// Target Coordinates to move to. This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<EntityCoordinates> TargetKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField]
    public StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";

    /// <summary>
    /// How close we need to get before considering movement finished.
    /// </summary>
    [DataField]
    public StateKey<float> RangeKey = "MovementRange";

    /// <summary>
    /// Do we only need to move into line of sight.
    /// </summary>
    [DataField]
    public bool StopOnLineOfSight;
}

public sealed class MoveToSystem : GoapActionSystem<MoveTo>
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action) => 2f;

    protected override async void ActionStartup(Entity<GoapComponent> ent, MoveTo action)
    {
        var state = ent.Comp.State;

        if (!state.TryGetValue(action.TargetKey, out var targetCoordinates))
            return;

        var owner = state.GetValue(GoapState.Owner);
        var xform = Transform(owner);
        var range = state.GetValueOrDefault(action.RangeKey);

        // In range
        if (xform.Coordinates.TryDistance(EntityManager, targetCoordinates, out var distance) && distance <= range)
            return;

        if (!action.PathfindInPlanning)
            return;

        var path = await _pathfinding.GetPath(
            owner,
            xform.Coordinates,
            targetCoordinates,
            range,
            default,
            flags: _pathfinding.GetFlags(state));

        if (path.Result != PathResult.Path)
            return;

        state.SetValue(action.PathfindKey, path);
        var comp = _steering.Register(owner, targetCoordinates);
        comp.ArriveOnLineOfSight = action.StopOnLineOfSight;

        if (Goap.TryGetValue(state, GoapState.OwnerCoordinates, out var coords))
        {
            var mapCoords = _transform.ToMapCoordinates(coords);
            _steering.PrunePath(
                owner,
                mapCoords,
                _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, path.Path);
        }

        comp.CurrentPath = new Queue<PathPoly>(path.Path);
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
    {
        var owner = ent.Comp.State.GetValue(GoapState.Owner);

        if (!TryComp<NPCSteeringComponent>(owner, out var steering))
            return GoapActionResult.Failed;

        return steering.Status switch
        {
            SteeringStatus.InRange => GoapActionResult.Finished,
            SteeringStatus.NoPath => GoapActionResult.Failed,
            SteeringStatus.Moving => GoapActionResult.Continuing,
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        // OwnerCoordinates is only used in planning so dump it.
        var state = ent.Comp.State;
        state.Remove(action.PathfindKey);

        if (action.RemoveKeyOnFinish)
            state.Remove(action.TargetKey);

        _steering.Unregister(state.GetValue(GoapState.Owner));
    }
}
