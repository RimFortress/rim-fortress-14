using System.Threading.Tasks;
using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using JetBrains.Annotations;
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
    public StateKey<float> RangeKey = GoapState.MovementRange;

    /// <summary>
    /// Do we only need to move into line of sight?
    /// </summary>
    [DataField]
    public bool StopOnLineOfSight;
}

/// <summary>
/// Manages <see cref="MoveTo"/> operator and also provides out-of-the-box AI movement logic for other operators.
/// </summary>
public sealed class MoveToActionSystem : GoapActionSystem<MoveTo>
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly PathfindingSystem _pathfinding = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityQuery<NPCSteeringComponent> _steeringQuery = default!;

    private readonly Dictionary<EntityUid, Task<PathResultEvent>> _pendingPaths = new();

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MoveTo action)
        => TryGetValue(ent, action, action.TargetKey, out var targetCoords) && StartupMovement(ent,
            action,
            targetCoords,
            action.FindPath,
            action.PathfindKey,
            action.RangeKey,
            action.StopOnLineOfSight);

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
        => !TryGetValue(ent, action, action.TargetKey, out var targetCoords)
            ? GoapActionResult.Failed
            : UpdateMovement(ent, action, targetCoords, action.PathfindKey, action.RangeKey, action.StopOnLineOfSight);

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        if (action.RemoveKeyOnFinish)
            ent.Comp.State.Remove(action.TargetKey);
        ShutdownMovement(ent, action.PathfindKey);
    }

    /// <summary>
    /// Initializes the pathfinding for AI movement.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="targetCoordinates">Target Coordinates to move to.</param>
    /// <param name="findPath">Should we search for a path to the target, or use the existing one?</param>
    /// <param name="pathfindKey">Where the pathfinding result will be stored (if applicable).</param>
    /// <param name="rangeKey">How close we need to get before considering movement finished.</param>
    /// <param name="stopOnLineOfSight">Do we only need to move into line of sight?</param>
    /// <returns>True, if initialization was successful.</returns>
    [PublicAPI]
    public bool StartupMovement(
        Entity<GoapComponent> ent,
        GoapAction action,
        EntityCoordinates targetCoordinates,
        bool findPath,
        StateKey<PathResultEvent> pathfindKey,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false)
        => TryGetValue(ent, action, rangeKey, out var range)
           && StartupMovement(ent, action, targetCoordinates, findPath, pathfindKey, range, stopOnLineOfSight);

    [PublicAPI]
    public bool StartupMovement(
        Entity<GoapComponent> ent,
        GoapAction action,
        EntityCoordinates targetCoordinates,
        bool findPath,
        StateKey<PathResultEvent> pathfindKey,
        float range,
        bool stopOnLineOfSight = false)
    {
        var xform = Transform(ent);

        if (xform.Coordinates.TryDistance(EntityManager, targetCoordinates, out var distance) && distance <= range)
            return true;

        // If there's no need to search for a path, we'll use the existing one.
        if (!findPath)
        {
            if (!TryGetValue(ent, action, pathfindKey, out var path))
                return false;

            var mapCoords = _transform.ToMapCoordinates(Goap.GetValue(ent.Comp.State, GoapState.OwnerCoordinates));
            _steering.PrunePath(ent, mapCoords, _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position, path.Path);
            _steering.Register(ent, targetCoordinates).ArriveOnLineOfSight = stopOnLineOfSight;
            return true;
        }

        // Start the path search in the background.
        _pendingPaths[ent] = _pathfinding.GetPath(
            ent,
            xform.Coordinates,
            targetCoordinates,
            range,
            default,
            flags: _pathfinding.GetFlags(ent.Comp.State));

        CreateDump(ent, action, $"path search started at {_timing.CurTime}. Target coordinates: {targetCoordinates}, Range: {range}");
        return true;
    }

    /// <summary>
    /// Updates the AI movement.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="action">GOAP action.</param>
    /// <param name="targetCoordinates">Target Coordinates to move to.</param>
    /// <param name="pathfindKey">Where the pathfinding result will be stored (if applicable).</param>
    /// <param name="rangeKey">How close we need to get before considering movement finished.</param>
    /// <param name="stopOnLineOfSight">Do we only need to move into line of sight?</param>
    /// <returns>
    /// <see cref="GoapActionResult.Finished"/> if the movement is finished,
    /// <see cref="GoapActionResult.Continuing"/> if it is in progress,
    /// <see cref="GoapActionResult.Failed"/> if the movement failed.
    /// </returns>
    [PublicAPI]
    public GoapActionResult UpdateMovement(
        Entity<GoapComponent> ent,
        GoapAction action,
        EntityCoordinates targetCoordinates,
        StateKey<PathResultEvent> pathfindKey,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false)
        => TryGetValue(ent, action, rangeKey, out var range)
            ? UpdateMovement(ent, action, targetCoordinates, pathfindKey, range, stopOnLineOfSight)
            : GoapActionResult.Failed;

    [PublicAPI]
    public GoapActionResult UpdateMovement(
        Entity<GoapComponent> ent,
        GoapAction action,
        EntityCoordinates targetCoordinates,
        StateKey<PathResultEvent> pathfindKey,
        float range,
        bool stopOnLineOfSight = false)
    {
        if (_pendingPaths.TryGetValue(ent, out var pathTask))
        {
            // Waiting for the asynchronous path search to complete.
            if (!pathTask.IsCompleted)
                return GoapActionResult.Continuing;

            _pendingPaths.Remove(ent);

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

            ent.Comp.State.SetValue(pathfindKey, path);

            var steering = _steering.Register(ent, targetCoordinates);
            steering.ArriveOnLineOfSight = stopOnLineOfSight;
            steering.CurrentPath = new Queue<PathPoly>(path.Path);

            // Launch steering along the found path.
            if (TryGetValue(ent, action, GoapState.OwnerCoordinates, out var coords))
            {
                var mapCoords = _transform.ToMapCoordinates(coords);
                _steering.PrunePath(
                    ent,
                    mapCoords,
                    _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position,
                    path.Path);
            }

            CreateDump(ent, action, "path found and steering initialized");
            return GoapActionResult.Continuing;
        }

        if (Transform(ent).Coordinates.TryDistance(EntityManager, targetCoordinates, out var distance)
            && distance <= range)
            return GoapActionResult.Finished;

        if (!_steeringQuery.TryComp(ent, out var steeringComp))
        {
            ComponentNotFound<NPCSteeringComponent>(ent, action);
            return GoapActionResult.Failed;
        }

        switch (steeringComp.Status)
        {
            case SteeringStatus.InRange:
                CreateDump(ent, action, $"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Finished;
            case SteeringStatus.Moving:
                return GoapActionResult.Continuing;
            case SteeringStatus.NoPath:
                CreateDump(ent, action, $"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Failed;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Finishes the AI's movement and performs the necessary state cleanup.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="pathfindKey">Where the pathfinding result will be stored (if applicable).</param>
    /// <param name="unregisterSteering"><see cref="NPCSteeringSystem.Unregister"/></param>
    [PublicAPI]
    public void ShutdownMovement(
        Entity<GoapComponent> ent,
        StateKey<PathResultEvent> pathfindKey,
        bool unregisterSteering = true)
    {
        _pendingPaths.Remove(ent);
        ent.Comp.State.Remove(pathfindKey);
        if (unregisterSteering)
            _steering.Unregister(ent);
    }

    /// <summary>
    /// Checks whether movement has been initialized for this AI.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    [PublicAPI]
    public bool StartedUp(Entity<GoapComponent> ent)
        => _pendingPaths.ContainsKey(ent) || HasComp<NPCSteeringComponent>(ent);
}
