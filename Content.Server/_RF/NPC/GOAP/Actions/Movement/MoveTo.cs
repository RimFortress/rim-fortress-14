using System.Threading.Tasks;
using Content.Server.NPC.Components;
using Content.Server.NPC.Pathfinding;
using Content.Server.NPC.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
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
    public StateKey<PathResultEvent> PathfindKey = MoveToActionSystem.PathfindKey;

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
public sealed partial class MoveToActionSystem : GoapActionSystem<MoveTo>
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private NPCSteeringSystem _steering = default!;
    [Dependency] private PathfindingSystem _pathfinding = default!;
    [Dependency] private TransformSystem _transform = default!;
    [Dependency] private EntityQuery<NPCSteeringComponent> _steeringQuery;

    /// <summary>
    /// The base key for storing the path that the agent must follow.
    /// </summary>
    [PublicAPI]
    public static readonly StateKey<PathResultEvent> PathfindKey = "MovementPathfinding";

    private readonly Dictionary<EntityUid, Task<PathResultEvent>> _pendingPaths = new();

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action) => 2f;

    protected override bool ActionStartup(Entity<GoapComponent> ent, MoveTo action)
        => TryGet(ent, action.TargetKey, out var targetCoords) && StartupMovement(ent,
            this,
            targetCoords,
            action.FindPath,
            action.PathfindKey,
            action.RangeKey,
            action.StopOnLineOfSight);

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
        => TryGet(ent, action.TargetKey, out var targetCoords)
            ? UpdateMovement(ent, this, targetCoords, action.PathfindKey, action.RangeKey, action.StopOnLineOfSight)
            : GoapActionResult.Failed;

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        if (action.RemoveKeyOnFinish)
            Remove(ent, action.TargetKey);

        ShutdownMovement(ent, this, action.PathfindKey);
    }

    /// <summary>
    /// Initializes the pathfinding for AI movement.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="handler"></param>
    /// <param name="targetCoordinates">Target Coordinates to move to.</param>
    /// <param name="findPath">Should we search for a path to the target, or use the existing one?</param>
    /// <param name="pathfindKey">Where the pathfinding result will be stored (if applicable).</param>
    /// <param name="rangeKey">How close we need to get before considering movement finished.</param>
    /// <param name="stopOnLineOfSight">Do we only need to move into line of sight?</param>
    /// <returns>True, if initialization was successful.</returns>
    [PublicAPI]
    public bool StartupMovement(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityCoordinates targetCoordinates,
        bool findPath,
        StateKey<PathResultEvent> pathfindKey,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false)
        => TryGet(ent, rangeKey, out var range)
           && StartupMovement(ent, handler, targetCoordinates, findPath, pathfindKey, range, stopOnLineOfSight);

    [PublicAPI]
    public bool StartupMovement(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
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
            if (!handler.TryGet(ent, pathfindKey, out var path))
                return false;

            var mapCoords = _transform.ToMapCoordinates(Get(ent, GoapState.OwnerCoordinates));
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

        handler.CreateDump($"path search started at {_timing.CurTime}. Target coordinates: {targetCoordinates}, Range: {range}");
        return true;
    }

    /// <summary>
    /// Updates the AI movement.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="handler"></param>
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
        GoapDebugDumpSystem handler,
        EntityCoordinates targetCoordinates,
        StateKey<PathResultEvent> pathfindKey,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false)
        => TryGet(ent, rangeKey, out var range)
            ? UpdateMovement(ent, handler, targetCoordinates, pathfindKey, range, stopOnLineOfSight)
            : GoapActionResult.Failed;

    [PublicAPI]
    public GoapActionResult UpdateMovement(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
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
                handler.CreateDump("background async pathfinding failed");
                return GoapActionResult.Failed;
            }

            var path = pathTask.GetAwaiter().GetResult();
            handler.CreateDump($"pathfinding finished at {_timing.CurTime}");

            if (path.Result != PathResult.Path)
            {
                handler.CreateDump($"pathfinding returned {path.Result}");
                return GoapActionResult.Failed;
            }

            ent.Comp.State.SetValue(pathfindKey, path);

            var steering = _steering.Register(ent, targetCoordinates);
            steering.ArriveOnLineOfSight = stopOnLineOfSight;
            steering.CurrentPath = new Queue<PathPoly>(path.Path);

            // Launch steering along the found path.
            if (handler.TryGet(ent, GoapState.OwnerCoordinates, out var coords))
            {
                var mapCoords = _transform.ToMapCoordinates(coords);
                _steering.PrunePath(
                    ent,
                    mapCoords,
                    _transform.ToMapCoordinates(targetCoordinates).Position - mapCoords.Position,
                    path.Path);
            }

            handler.CreateDump("path found and steering initialized");
            return GoapActionResult.Continuing;
        }

        if (Transform(ent).Coordinates.TryDistance(EntityManager, targetCoordinates, out var distance)
            && distance <= range)
            return GoapActionResult.Finished;

        if (!_steeringQuery.TryComp(ent, out var steeringComp))
        {
            ComponentNotFound<NPCSteeringComponent>();
            return GoapActionResult.Failed;
        }

        switch (steeringComp.Status)
        {
            case SteeringStatus.InRange:
                handler.CreateDump($"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Finished;
            case SteeringStatus.Moving:
                return GoapActionResult.Continuing;
            case SteeringStatus.NoPath:
                handler.CreateDump($"steering finished with result: '{steeringComp.Status}'");
                return GoapActionResult.Failed;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    /// <summary>
    /// Finishes the AI's movement and performs the necessary state cleanup.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="handler"></param>
    /// <param name="pathfindKey">Where the pathfinding result will be stored (if applicable).</param>
    /// <param name="unregisterSteering"><see cref="NPCSteeringSystem.Unregister"/></param>
    [PublicAPI]
    public void ShutdownMovement(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        StateKey<PathResultEvent> pathfindKey,
        bool unregisterSteering = true)
    {
        _pendingPaths.Remove(ent);
        handler.Remove(ent, pathfindKey);
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

    /// <summary>
    /// Implements all the logic for moving the agent to the target coordinates.
    /// </summary>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityCoordinates targetCoordinates,
        StateKey<float> rangeKey,
        StateKey<PathResultEvent>? pathfindKey = null,
        bool findPath = true,
        bool stopOnLineOfSight = false,
        bool unregisterSteering = true)
    {
        if (!targetCoordinates.TryDistance(EntityManager, _transform, Transform(ent).Coordinates, out var dist)
            || !handler.TryGet(ent, rangeKey, out var range))
            return GoapActionResult.Failed;

        pathfindKey ??= PathfindKey;

        if (dist > range)
        {
            if (!StartedUp(ent))
                StartupMovement(ent, handler, targetCoordinates, true, pathfindKey.Value, rangeKey);

            var result = UpdateMovement(ent, handler, targetCoordinates, pathfindKey.Value, rangeKey);

            if (result != GoapActionResult.Finished)
                return result;
        }

        if (StartedUp(ent))
            ShutdownMovement(ent, handler, pathfindKey.Value, unregisterSteering: unregisterSteering);

        return GoapActionResult.Finished;
    }
}
