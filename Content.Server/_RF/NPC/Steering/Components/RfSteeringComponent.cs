using System.Numerics;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Steering.Components;

/// <summary>
/// A component that moves the agent along a path. It exists only while the agent is following one.
/// </summary>
/// <remarks>
/// All of the positions are in the coordinates of the grid of the path, which is the frame the input of the mover is
/// given in when the agent is standing on that grid.
/// </remarks>
[RegisterComponent]
public sealed partial class RfSteeringComponent : Component
{
    /// <summary>
    /// The grid the path is on.
    /// </summary>
    [ViewVariables]
    public EntityUid Graph;

    /// <summary>
    /// The regions of the nodes of the path, from the start to the end.
    /// </summary>
    [ViewVariables]
    public List<Box2> Nodes = new();

    /// <summary>
    /// The distance from the center of each node to the end of the path along it.
    /// </summary>
    [ViewVariables]
    public List<float> Remaining = new();

    /// <summary>
    /// Where the path ends.
    /// </summary>
    [ViewVariables]
    public Vector2 End;

    /// <summary>
    /// The node the agent is at.
    /// </summary>
    [ViewVariables]
    public int Index;

    /// <summary>
    /// The region the end of the path is the edge of, if the agent goes to a body: the range is counted from it.
    /// </summary>
    [ViewVariables]
    public Box2? Bounds;

    /// <summary>
    /// A body that the agent is not to avoid, it is the target of the path and the agent is to come up to it.
    /// </summary>
    [ViewVariables]
    public EntityUid? Ignore;

    /// <summary>
    /// If it is set, the agent has got there when it sees these coordinates for a while, it need not come up to them.
    /// </summary>
    [ViewVariables]
    public EntityCoordinates? Sight;

    /// <summary>
    /// How long the <see cref="Sight"/> has been seen.
    /// </summary>
    public float SightSeen;

    /// <summary>
    /// How close to the <see cref="End"/> the agent needs to get.
    /// </summary>
    [ViewVariables]
    public float Range;

    /// <summary>
    /// How the movement is going.
    /// </summary>
    [ViewVariables]
    public RfSteeringStatus Status;

    /// <summary>
    /// The velocity the agent wants to have, the result of the last decision.
    /// </summary>
    [ViewVariables]
    public Vector2 Desired;

    /// <summary>
    /// The tick the next decision is made on.
    /// </summary>
    [ViewVariables]
    public GameTick NextDecision;

    /// <summary>
    /// How much the velocity changes for a unit of the input in a second, as it was measured. It depends on the
    /// floor and the state of the mob, so it is not taken from anywhere.
    /// </summary>
    [ViewVariables]
    public float Authority;

    /// <summary>
    /// The radius of the agent, taken from its fixtures.
    /// </summary>
    [ViewVariables]
    public float Radius;

    /// <summary>
    /// Whether the steering made the mover leave the rotation of the agent alone, in which case it turns the agent
    /// itself: the mover turns it to the input, and the input is against the movement when the agent brakes.
    /// </summary>
    public bool AddedNoRotate;

    /// <summary>
    /// The side the agent is going around a body by, -1 to the right and 1 to the left, 0 if none. It is kept for a
    /// while so that the agent does not change its mind every decision.
    /// </summary>
    public int AvoidSide;

    /// <summary>
    /// Until when the <see cref="AvoidSide"/> is kept.
    /// </summary>
    public TimeSpan AvoidUntil;

    /// <summary>
    /// The acceleration of the floor the <see cref="Authority"/> is of, relative to the usual one.
    /// </summary>
    public float Floor;

    /// <summary>
    /// How many times the <see cref="Authority"/> has been measured.
    /// </summary>
    public int Samples;

    /// <summary>
    /// The velocity and the input of the last tick, to measure the <see cref="Authority"/>.
    /// </summary>
    public Vector2 LastVelocity;

    /// <summary>
    /// See <see cref="LastVelocity"/>.
    /// </summary>
    public Vector2 LastInput;

    /// <summary>
    /// Where the agent was when the <see cref="StuckSince"/> started.
    /// </summary>
    public Vector2 StuckPosition;

    /// <summary>
    /// The least distance to the end of the path that the agent has been at, and when it was last made smaller.
    /// </summary>
    public float BestRemaining = float.MaxValue;

    /// <summary>
    /// See <see cref="BestRemaining"/>.
    /// </summary>
    public TimeSpan ProgressSince;

    /// <summary>
    /// When the agent last made progress.
    /// </summary>
    public TimeSpan StuckSince;
}

public enum RfSteeringStatus : byte
{
    /// <summary>
    /// The agent is going to the end of the path.
    /// </summary>
    Moving,

    /// <summary>
    /// The agent has reached the end and stopped.
    /// </summary>
    InRange,

    /// <summary>
    /// The agent does not get anywhere, the path has to be searched for again.
    /// </summary>
    Stuck,

    /// <summary>
    /// The agent is too far from the path, it has to be searched for again.
    /// </summary>
    OffPath,

    /// <summary>
    /// The movement was cancelled, the agent is braking and will be released as soon as it has stopped.
    /// </summary>
    Stopping,
}
