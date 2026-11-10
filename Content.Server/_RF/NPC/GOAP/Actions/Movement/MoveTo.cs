using System.Numerics;
using Content.Server._RF.NPC.Pathfinding;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Server._RF.NPC.Steering.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using JetBrains.Annotations;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Actions.Movement;

/// <summary>
/// Moves an NPC to the specified target key. The path is found and followed by the RimFortress pathfinding
/// and steering.
/// </summary>
public sealed partial class MoveTo : BaseGoapAction<MoveTo>
{
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
    /// The entity to move to, if it is set, the <see cref="TargetKey"/> is not used. The agent comes up to the region
    /// the entity takes up and not to its center, so it can reach the entities that are big and solid.
    /// The entity is followed if it moves.
    /// </summary>
    [DataField]
    public StateKey<EntityUid>? TargetEntityKey;

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

    /// <summary>
    /// What the path is searched for with, null for the default of the agent. The rules of the profiles of the agent can
    /// be different for different queries.
    /// </summary>
    [DataField]
    public ProtoId<PathQueryPrototype>? Query;

    /// <summary>
    /// What the path keeps away from, the places are taken from the results of the query every time the path is searched for.
    /// </summary>
    [DataField]
    public PathAvoidSpec? Avoid;

    /// <summary>
    /// True to run, false to walk, null for the default of the agent: it runs in the combat mode and walks when it is not in it. The agent
    /// that runs from the enemies or goes to fight is not in the combat mode yet, so the action says so.
    /// </summary>
    [DataField]
    public bool? Sprint;
}

/// <summary>
/// Manages <see cref="MoveTo"/> operator and also provides out-of-the-box AI movement logic for other operators.
/// </summary>
public sealed partial class MoveToActionSystem : GoapActionSystem<MoveTo>
{
    [Dependency] private RfSteeringSystem _steering = null!;
    [Dependency] private RfPathfindingSystem _pathfinding = null!;
    [Dependency] private EntityQuery<RfSteererComponent> _steererQuery;

    protected override float ActionCost(Entity<GoapComponent> ent, GoapState state, MoveTo action) => 2f;

    protected override GoapActionResult ActionUpdate(Entity<GoapComponent> ent, MoveTo action)
    {
        if (!TryGet(ent, action.RangeKey, out var range))
            return GoapActionResult.Failed;

        if (action.TargetEntityKey is { } entityKey)
        {
            return TryGet(ent, entityKey, out var target)
                ? Move(ent, this, target, range, action.StopOnLineOfSight, action.Query, _pathfinding.GetAvoid(ent, action.Avoid), action.Sprint)
                : GoapActionResult.Failed;
        }

        return TryGet(ent, action.TargetKey, out var coordinates)
            ? Move(ent, this, coordinates, range, action.StopOnLineOfSight, action.Query, _pathfinding.GetAvoid(ent, action.Avoid), action.Sprint)
            : GoapActionResult.Failed;
    }

    protected override void ActionShutdown(Entity<GoapComponent> ent, MoveTo action)
    {
        // The entity is not removed, it is not the key of this action, other actions use it.
        if (action is { RemoveKeyOnFinish: true, TargetEntityKey: null })
            Remove(ent, action.TargetKey);

        Stop(ent);
    }

    /// <summary>
    /// Moves the agent to the coordinates. Call it every update until it is over: the path is found on the first
    /// call, and then the agent follows it, finds it again if it has to, and is stopped when it has got there.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="handler">The system whose dump gets the messages.</param>
    /// <param name="target">Target Coordinates to move to.</param>
    /// <param name="rangeKey">How close we need to get before considering movement finished.</param>
    /// <param name="stopOnLineOfSight">Do we only need to move into line of sight?</param>
    /// <param name="query">What the path is searched for with, null for the default of the agent.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Finished"/> if the movement is finished,
    /// <see cref="GoapActionResult.Continuing"/> if it is in progress,
    /// <see cref="GoapActionResult.Failed"/> if the movement failed.
    /// </returns>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityCoordinates target,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false,
        ProtoId<PathQueryPrototype>? query = null)
        => handler.TryGet(ent, rangeKey, out var range)
            ? Move(ent, handler, target, range, stopOnLineOfSight, query)
            : GoapActionResult.Failed;

    /// <inheritdoc cref="Move(Entity{GoapComponent}, GoapDebugDumpSystem, EntityCoordinates, StateKey{float}, bool, ProtoId{PathQueryPrototype}?)"/>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityCoordinates target,
        float range,
        bool stopOnLineOfSight = false,
        ProtoId<PathQueryPrototype>? query = null,
        IReadOnlyList<PathAvoid>? avoid = null,
        bool? sprint = null)
        => Report(handler, _steering.Move(ent, target, range, stopOnLineOfSight, query, avoid, sprint));

    /// <summary>
    /// Moves the agent to the entity. The agent comes up to the region the entity takes up and not to its center,
    /// so the entities that are big and solid can be reached, and the range does not have to be made bigger for them.
    /// The entity is followed if it moves.
    /// </summary>
    /// <inheritdoc cref="Move(Entity{GoapComponent}, GoapDebugDumpSystem, EntityCoordinates, StateKey{float}, bool, ProtoId{PathQueryPrototype}?)"/>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityUid target,
        StateKey<float> rangeKey,
        bool stopOnLineOfSight = false,
        ProtoId<PathQueryPrototype>? query = null)
        => handler.TryGet(ent, rangeKey, out var range)
            ? Move(ent, handler, target, range, stopOnLineOfSight, query)
            : GoapActionResult.Failed;

    /// <inheritdoc cref="Move(Entity{GoapComponent}, GoapDebugDumpSystem, EntityUid, StateKey{float}, bool, ProtoId{PathQueryPrototype}?)"/>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> ent,
        GoapDebugDumpSystem handler,
        EntityUid target,
        float range,
        bool stopOnLineOfSight = false,
        ProtoId<PathQueryPrototype>? query = null,
        IReadOnlyList<PathAvoid>? avoid = null,
        bool? sprint = null)
        => Report(handler, _steering.Move(ent, target, range, stopOnLineOfSight, query, avoid, sprint));

    /// <summary>
    /// Finishes the AI's movement. The agent brakes and the path request is cancelled.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    [PublicAPI]
    public void Stop(EntityUid ent) => _steering.Cancel(ent);

    /// <summary>
    /// Checks whether the agent is carrying out a movement order.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    [PublicAPI]
    public bool IsMoving(EntityUid ent)
        => _steererQuery.TryComp(ent, out var steerer) && steerer.Phase != RfMovePhase.None;

    /// <summary>
    /// Makes a short step to a place close by, with no path searched for,
    /// if the way there is clear. Stop it with <see cref="Stop"/>.
    /// </summary>
    /// <param name="ent">AI entity.</param>
    /// <param name="target">Where to step, in the world coordinates.</param>
    /// <param name="face">The entity the agent keeps facing on the way.</param>
    /// <returns>False if the step is not possible.</returns>
    [PublicAPI]
    public bool Step(EntityUid ent, Vector2 target, EntityUid? face = null) => _steering.Step(ent, target, face);

    /// <summary>
    /// Checks whether the agent is making a <see cref="Step"/>.
    /// </summary>
    [PublicAPI]
    public bool IsStepping(EntityUid ent) => _steering.IsStepping(ent);

    private static GoapActionResult Report(GoapDebugDumpSystem handler, GoapActionResult result)
    {
        if (result == GoapActionResult.Failed)
            handler.CreateDump("movement failed: no path, or the agent cannot get there");

        return result;
    }
}
