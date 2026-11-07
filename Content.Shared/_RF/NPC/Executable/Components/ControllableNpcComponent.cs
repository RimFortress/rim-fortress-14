using Content.Shared._RF.NPC.Executable.Prototypes;
using Content.Shared._RF.NPC.Executable.Systems;
using System.Numerics;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._RF.NPC.Executable.Components;

/// <summary>
/// Npc that can be controlled by the player
/// </summary>
[Access(typeof(SharedExecutableGoalSystem))]
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ControllableNpcComponent : Component
{
    /// <summary>
    /// Entities that can control this npc.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public readonly HashSet<EntityUid> CanControl = new();

    /// <summary>
    /// Conditions that must be met in order for the controller to change an entity's combat mode.
    /// </summary>
    [DataField]
    public List<GoapCondition> CombatConditions = new();

    /// <summary>
    /// Goals that can be assigned to this entity.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public List<ProtoId<ExecutableGoalPrototype>> Goals = new();

    /// <summary>
    /// Will the queue be cleared if even one of the goals fails?
    /// </summary>
    [DataField]
    public bool ClearQueueOnFail;

    [DataField]
    public int QueueMaxCapacity = int.MaxValue;

    /// <summary>
    /// A queue of goals for the agent to complete.
    /// </summary>
    [ViewVariables, AutoNetworkedField]
    public Queue<ExecutableGoalQueueEntry> Queue = new();

    [ViewVariables, AutoNetworkedField]
    public ProtoId<ExecutableGoalPrototype>? CurrentGoal;

    [ViewVariables, AutoNetworkedField]
    public EntityUid? CurrentTarget;

    [ViewVariables, AutoNetworkedField]
    public EntityCoordinates? CurrentTargetCoordinates;

    /// <summary>
    /// The path that the agent follows, for the overlay of the orders to draw the way and not a straight line. It is not networked as
    /// the other fields are: they are sent to everybody who sees the agent, and the way of an agent is for those who control it. The server tells
    /// them when the path is changed (see <see cref="NpcRouteMessage"/>), and the client keeps what it was told here.
    /// </summary>
    [ViewVariables]
    public List<Vector2> Route = new();

    /// <summary>
    /// The grid that the points of the <see cref="Route"/> are in the coordinates of.
    /// </summary>
    [ViewVariables]
    public EntityUid RouteGraph;

    /// <summary>
    /// On the client: when the path was asked for last, so it is not asked for again and again.
    /// </summary>
    [ViewVariables]
    public TimeSpan RouteAskedAt;
}

/// <summary>
/// Executable goal queue entry.
/// </summary>
/// <param name="Goal">Executable goal prototype.</param>
/// <param name="User">The user who added this entry to the queue.</param>
/// <param name="Target">Target entity of the goal, if any.</param>
/// <param name="TargetCoordinates">Target coordinates of the goal, if any.</param>
[Serializable, NetSerializable]
public readonly record struct ExecutableGoalQueueEntry(
    ProtoId<ExecutableGoalPrototype> Goal,
    NetEntity User,
    NetEntity? Target,
    NetCoordinates? TargetCoordinates);

/// <summary>
/// Tells the path of an agent to those who control it, and only to them. It is sent when the path is changed.
/// </summary>
/// <param name="Agent">The agent.</param>
/// <param name="Graph">The grid that the points are in the coordinates of.</param>
/// <param name="Points">The points of the path, none if the agent follows no path.</param>
[Serializable, NetSerializable]
public sealed class NpcRouteMessage(NetEntity agent, NetEntity graph, List<Vector2> points) : EntityEventArgs
{
    public readonly NetEntity Agent = agent;
    public readonly NetEntity Graph = graph;
    public readonly List<Vector2> Points = points;
}

/// <summary>
/// Asks for the path of an agent: the one who controls it has not got it, such as when the agent has come into view.
/// </summary>
[Serializable, NetSerializable]
public sealed class NpcRouteRequest(NetEntity agent) : EntityEventArgs
{
    public readonly NetEntity Agent = agent;
}
