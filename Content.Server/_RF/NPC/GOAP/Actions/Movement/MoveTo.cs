using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Moves an NPC to the specified target key. Hands the actual steering off to NPCSystem.Steering.
/// </summary>
public sealed partial class MoveTo : BaseGoapAction<MoveTo>
{
    /// <summary>
    /// Should we search for a path to the target, or use the existing one?
    /// </summary>
    [DataField]
    public bool FindPath = true;

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
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery;

    private readonly Dictionary<EntityUid, Task<PathResultEvent>> _pendingPaths = new();

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MoveTo action)
    {
        var state = ent.Comp.State;

        if (!Goap.TryGetValue(state, action.TargetKey, out var targetCoordinates))
        {
            KeyNotFound(ent, action, action.TargetKey);
            return false;
        }

        var owner = state.GetValue(GoapState.Owner);
        var xform = Transform(owner);
        var range = state.GetValueOrDefault(action.RangeKey);

        if (xform.Coordinates.TryDistance(EntityManager, targetCoordinates, out var distance) && distance <= range)
            return true;

        // If there's no need to search for a path, we'll use the existing one.
        if (!action.FindPath)
        {
            if (!state.TryGetValue(action.PathfindKey, out var path))
            {
                KeyNotFound(ent, action, action.PathfindKey);
                return false;
            }

            var mapCoords = _transform.ToMapCoordinates(state.GetValue(GoapState.OwnerCoordinates));
            _steering.PrunePath(owner, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, path.Path);
            return true;
        }

        // Start the path search in the background.
        _pendingPaths[owner] = _pathfinding.GetPath(
            owner,
            xform.Coordinates,
            targetCoordinates,
            range,
            default,
            flags: _pathfinding.GetFlags(state));

        CreateDump(ent, action, $"path search started at {_timing.CurTime}. Target coordinates: {targetCoordinates}, Range: {range}");
        return true;
    }

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
    {
        var owner = ent.Comp.State.GetValue(GoapState.Owner);

        if (_pendingPaths.TryGetValue(owner, out var pathTask))
        {
            // Waiting for the asynchronous path search to complete.
            if (!pathTask.IsCompleted)
                return GoapActionResult.Continuing;

            _pendingPaths.Remove(owner);

            if (!pathTask.IsCompletedSuccessfully)
            {
                CreateDump(ent, action, "background async pathfinding failed");
                return GoapActionResult.Failed;
            }

            var path = pathTask.GetAwaiter().GetResult();
            CreateDump(ent, action, $"pathfinding finished at {_timing.CurTime}");

            if (path.Result != PathResult.Path)
            {
                CreateDump(ent, action, $"pathfinding returned {path.Result}");
                return GoapActionResult.Failed;
            }

            var state = ent.Comp.State;
            state.SetValue(action.PathfindKey, path);

            if (!_steeringQuery.TryComp(owner, out var steering))
                steering = _steering.Register(owner, state.GetValue(action.TargetKey));

            steering.ArriveOnLineOfSight = action.StopOnLineOfSight;
            steering.CurrentPath = new Queue<PathPoly>(path.Path);

            // Launch steering along the found path.
            if (Goap.TryGetValue(state, GoapState.OwnerCoordinates, out var coords))
            {
                var mapCoords = _transform.ToMapCoordinates(coords);
                _steering.PrunePath(
                    owner,
                    mapCoords,
                    _transform.ToMapCoordinates(state.GetValue(action.TargetKey)).Position - mapCoords.Position,
                    path.Path);
            }

            CreateDump(ent, action, "path found and steering initialized");
            return GoapActionResult.Continuing;
        }

        if (!_steeringQuery.TryComp(owner, out var steeringComp))
        {
            ComponentNotFound<NPCSteeringComponent>(ent, action);
            return GoapActionResult.Failed;
        }

        switch (steeringComp.Status)
        {
            case SteeringStatus.InRange:
                CreateDump(ent, action, "steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Finished;
            case SteeringStatus.Moving:
                return GoapActionResult.Continuing;
            case SteeringStatus.NoPath:
                CreateDump(ent, action, "steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Failed;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        var state = ent.Comp.State;
        var owner = state.GetValue(GoapState.Owner);

        _pendingPaths.Remove(owner);
        state.Remove(action.PathfindKey);

        if (action.RemoveKeyOnFinish)
            state.Remove(action.TargetKey);

        _steering.Unregister(owner);
    }
}
