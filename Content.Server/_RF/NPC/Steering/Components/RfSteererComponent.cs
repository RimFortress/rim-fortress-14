using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.Steering.Components;

/// <summary>
/// The settings of an agent that moves by itself and the state of its current movement order.
/// </summary>
/// <remarks>
/// The movement itself is done by <see cref="RfSteeringComponent"/>, which exists only while the agent is following a path.
/// </remarks>
[RegisterComponent]
public sealed partial class RfSteererComponent : Component
{
    /// <summary>
    /// How many ticks pass between the decisions of the agent, where to go and how fast. The input of the mover
    /// lasts for one tick, so it is given every tick regardless, from the last decision. The further the agent is
    /// from anyone who could see it, the less often it needs to decide.
    /// </summary>
    [DataField]
    public int UpdateInterval = 1;

    /// <summary>
    /// The share of the difference between the speed the agent wants and the one it has that it corrects in a tick.
    /// With one the agent gets the speed in a single tick, if it is more than one the speed oscillates, and the lower
    /// it is, the smoother the agent starts and stops.
    /// </summary>
    [DataField]
    public float Gain = 0.5f;

    /// <summary>
    /// The share of the sideways velocity that the agent counters in a tick. It is small because the mover turns the
    /// velocity to the input, and a strong correction to the side makes the agent twitch.
    /// </summary>
    [DataField]
    public float SideGain = 0.15f;

    /// <summary>
    /// The share of the distance left to the end of the path that the agent covers between two decisions when it is
    /// close to it. The agent keeps its speed until the last moment it can stop at, and this only keeps it from
    /// twitching around the end: the lower it is, the more gently the agent comes to a stop.
    /// </summary>
    [DataField]
    public float ApproachGain = 0.5f;

    /// <summary>
    /// How far from other bodies the agent keeps, besides the radii.
    /// </summary>
    [DataField]
    public float Clearance = 0.05f;

    /// <summary>
    /// How far from other agents the agent starts to move away from them, besides the radii, and how fast it does
    /// that when they are touching.
    /// </summary>
    [DataField]
    public float SeparationRange = 0.4f;

    /// <summary>
    /// See <see cref="SeparationRange"/>.
    /// </summary>
    [DataField]
    public float SeparationSpeed = 1.2f;

    /// <summary>
    /// The longest time ahead the agent looks for bodies in its way, the lookup grows with it.
    /// </summary>
    [DataField]
    public float AvoidHorizon = 1.5f;

    /// <summary>
    /// The share of the measured acceleration that the agent counts on when it plans to stop.
    /// </summary>
    [DataField]
    public float BrakeFactor = 0.6f;

    /// <summary>
    /// The speed below which the agent counts as stopped.
    /// </summary>
    [DataField]
    public float StopSpeed = 0.25f;

    /// <summary>
    /// How fast the agent turns to where it goes, in degrees a second.
    /// </summary>
    [DataField]
    public float TurnSpeed = 720f;

    /// <summary>
    /// How much farther than the range of the order the agent has to be pushed from the place it has arrived at to go
    /// back to it. Without it, an agent that is nudged back and forth around the border of the range keeps starting
    /// and stopping.
    /// </summary>
    [DataField]
    public float RestartMargin = 0.3f;

    /// <summary>
    /// When the last order was carried out.
    /// </summary>
    public TimeSpan FinishedAt;

    /// <summary>
    /// The agent is stuck if it gets no closer to the end of the path for this long, even if it keeps moving: it can go
    /// from side to side in front of something without ever getting by it.
    /// </summary>
    [DataField]
    public TimeSpan NoProgressTime = TimeSpan.FromSeconds(3);

    /// <summary>
    /// The agent has made progress if it is this much closer to the end of the path than it was.
    /// </summary>
    [DataField]
    public float ProgressDistance = 0.5f;

    /// <summary>
    /// How far from a place where the agent got stuck the paths that are searched for after it cost more, and how much.
    /// </summary>
    [DataField]
    public float AvoidRadius = 2.5f;

    /// <summary>
    /// See <see cref="AvoidRadius"/>.
    /// </summary>
    [DataField]
    public float AvoidCost = 40f;

    /// <summary>
    /// The paths are not made straighter during the order: the agent has got stuck on the way, and the path that is
    /// straighter than the one that was found is the one it got stuck on.
    /// </summary>
    public bool NoSmoothing;

    /// <summary>
    /// The places where the agent has got stuck during the order, the paths do not lead there again.
    /// </summary>
    public List<PathAvoid> Avoid = new();

    /// <summary>
    /// The agent is stuck if it does not move farther than <see cref="StuckDistance"/> for this long while it is told to move.
    /// </summary>
    [DataField]
    public TimeSpan StuckTime = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// See <see cref="StuckTime"/>.
    /// </summary>
    [DataField]
    public float StuckDistance = 0.3f;

    /// <summary>
    /// The agent has left the path if it is farther from the node it is heading to than this.
    /// </summary>
    [DataField]
    public float LeaveDistance = 2f;

    /// <summary>
    /// How far the target can move before the path to it is searched for again.
    /// </summary>
    [DataField]
    public float RetargetDistance = 2f;

    /// <summary>
    /// How many times the path is searched for again when the agent gets stuck or leaves it, before the order fails.
    /// </summary>
    [DataField]
    public int MaxRepaths = 3;

    /// <summary>
    /// What the agent is doing to carry out the order.
    /// </summary>
    [ViewVariables]
    public RfMovePhase Phase;

    /// <summary>
    /// Where the agent was told to go.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates Target;

    /// <summary>
    /// The entity the agent was told to go to, if it is not a place. The range is counted from its region.
    /// </summary>
    [ViewVariables]
    public EntityUid? TargetEntity;

    /// <summary>
    /// How close to the <see cref="Target"/> the agent was told to get.
    /// </summary>
    [ViewVariables]
    public float Range;

    /// <summary>
    /// The place the last path was searched for, to find out whether the target has moved.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates SearchedTarget;

    /// <summary>
    /// The goal at a node of the path that the agent is walking to, it is started when the agent gets there.
    /// </summary>
    [ViewVariables]
    public PathNodeGoal? PendingGoal;

    /// <summary>
    /// The goal of a path node that is being performed, the order is not carried out meanwhile.
    /// </summary>
    [ViewVariables]
    public ProtoId<UtilityAiGoalPrototype>? NodeGoal;

    /// <summary>
    /// The goal that was replaced by the <see cref="NodeGoal"/>, it is set again when that is finished.
    /// </summary>
    [ViewVariables]
    public ProtoId<UtilityAiGoalPrototype>? ReturnGoal;

    /// <summary>
    /// The region of the <see cref="TargetEntity"/> the last path was searched for, in the coordinates of the grid.
    /// </summary>
    [ViewVariables]
    public Box2? SearchedBounds;

    /// <summary>
    /// How many times the path has been searched for again during the order.
    /// </summary>
    [ViewVariables]
    public int Repaths;

    /// <summary>
    /// Whether the path being followed leads only to the closest reachable point to the target.
    /// </summary>
    [ViewVariables]
    public bool Partial;
}

public enum RfMovePhase : byte
{
    /// <summary>
    /// The agent has no order.
    /// </summary>
    None,

    /// <summary>
    /// The path is being searched for.
    /// </summary>
    Searching,

    /// <summary>
    /// The agent is following the path.
    /// </summary>
    Following,

    /// <summary>
    /// The agent has got to a node with a goal, the goal is about to be started.
    /// </summary>
    StartingGoal,
}
