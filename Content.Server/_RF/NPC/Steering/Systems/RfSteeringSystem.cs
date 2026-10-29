using System.Numerics;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Timing;

namespace Content.Server._RF.NPC.Steering.Systems;

/// <summary>
/// Moves the agents along the paths. The agent is steered by the velocity it has and the one it wants: the input is
/// the difference between them, so it speeds up, slows down and stops the same way on any floor, including the ones
/// the agent keeps sliding on.
/// </summary>
/// <remarks>
/// The decisions, where to go and how fast, are made once in <see cref="RfSteererComponent.UpdateInterval"/> ticks,
/// the input that follows from the last decision is given every tick.
/// </remarks>
public sealed partial class RfSteeringSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = null!;
    [Dependency] private SharedTransformSystem _transform = null!;
    [Dependency] private SharedMoverController _mover = null!;
    [Dependency] private EntityLookupSystem _lookup = null!;
    [Dependency] private SharedPhysicsSystem _physics = null!;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;
    [Dependency] private EntityQuery<MovementSpeedModifierComponent> _modifierQuery;

    // If the agent has no modifier, it moves like a mob without any.
    private const float DefaultSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
    private const float MinBrake = 1f;

    // The least the input changes the speed in a tick, in meters a second, and the most that is believed.
    private const float MinStep = 0.05f;
    private const float MaxAuthority = 400f;

    // How many nodes ahead the agent can be in, if it was thrown forward.
    private const int Lookahead = 4;

    // How fast the measure of the acceleration follows what is measured.
    private const float AuthorityRate = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesBefore.Add(typeof(SharedPhysicsSystem));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateOrders();

        var tick = _timing.CurTick;
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RfSteeringComponent, RfSteererComponent, InputMoverComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var steering, out var steerer, out var mover, out var xform))
        {
            Step(uid, steering, steerer, mover, xform, tick, now, frameTime);
        }
    }

    [SubscribeLocalEvent]
    private void OnSteeringShutdown(Entity<RfSteeringComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp(ent, out InputMoverComponent? mover))
            return;

        mover.CurTickSprintMovement = Vector2.Zero;

        var ev = new SpriteMoveEvent(false);
        RaiseLocalEvent(ent, ref ev);
    }

    private void Step(
        EntityUid uid,
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        InputMoverComponent mover,
        TransformComponent xform,
        GameTick tick,
        TimeSpan now,
        float frameTime)
    {
        if (!mover.CanMove || xform.ParentUid != steering.Graph || !_physicsQuery.TryComp(uid, out var physics))
        {
            if (steering.Status == RfSteeringStatus.Moving)
                steering.Status = xform.ParentUid != steering.Graph ? RfSteeringStatus.OffPath : RfSteeringStatus.Stuck;

            SetInput(mover, Vector2.Zero, tick);
            return;
        }

        _modifierQuery.TryComp(uid, out var modifier);

        // The input is given in the frame of the grid the mob stands on, and so are the positions.
        var frame = -_mover.GetParentGridAngle(mover);
        var velocity = frame.RotateVec(physics.LinearVelocity);
        var pos = xform.LocalPosition;

        MeasureAuthority(steering, modifier, velocity, frameTime);

        if (steering.Status == RfSteeringStatus.Stopping && velocity.Length() <= steerer.StopSpeed)
        {
            RemComp<RfSteeringComponent>(uid);
            return;
        }

        if (steering.Status == RfSteeringStatus.Moving && tick >= steering.NextDecision)
        {
            steering.NextDecision = tick + (uint) Math.Max(steerer.UpdateInterval, 1);
            Decide(uid, steering, steerer, modifier, mover, xform, pos, velocity, now, frameTime);
        }

        var input = GetInput(steering, steerer, modifier, velocity, frameTime);

        steering.LastVelocity = velocity;
        steering.LastInput = input;
        SetInput(mover, input, tick);
    }

    /// <summary>
    /// Decides where the agent is going and how fast.
    /// </summary>
    private void Decide(
        EntityUid uid,
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        MovementSpeedModifierComponent? modifier,
        InputMoverComponent mover,
        TransformComponent xform,
        Vector2 pos,
        Vector2 velocity,
        TimeSpan now,
        float frameTime)
    {
        var previous = steering.Desired;
        var nodes = steering.Nodes;
        var last = nodes.Count - 1;

        // The agent can be pushed ahead or knocked back, it is at the farthest of the nodes near it that it is in.
        var found = false;

        for (var i = Math.Min(last, steering.Index + Lookahead); i >= steering.Index; i--)
        {
            if (!nodes[i].Contains(pos))
                continue;

            steering.Index = i;
            found = true;
            break;
        }

        // Going around something takes the agent off the nodes, it moves on when it is past the middle of the way
        // to the next one.
        while (steering.Index < last)
        {
            var from = nodes[steering.Index].Center;
            var way = nodes[steering.Index + 1].Center - from;

            if (Vector2.Dot(pos - from, way) <= way.LengthSquared() * 0.5f)
                break;

            steering.Index++;
        }

        var node = nodes[steering.Index].Center;

        if (!found && (node - pos).Length() > steerer.LeaveDistance)
        {
            Finish(steering, RfSteeringStatus.OffPath);
            return;
        }

        var toEnd = (steering.End - pos).Length();

        if (toEnd <= steering.Range && velocity.Length() <= steerer.StopSpeed)
        {
            Finish(steering, RfSteeringStatus.InRange);
            return;
        }

        // The agent heads for the center of the next node, the last one is headed for the end of the path.
        Vector2 aim;
        float remaining;

        if (steering.Index >= last)
        {
            aim = steering.End;
            remaining = toEnd;
        }
        else
        {
            aim = nodes[steering.Index + 1].Center;
            remaining = (aim - pos).Length() + steering.Remaining[steering.Index + 1];
        }

        // The speed from which the agent is still able to stop in time. The end is aimed at a bit short of it,
        // so that the agent is in range when it stops.
        var margin = Math.Min(steering.Range * 0.5f, 0.5f);
        var brake = Math.Max(steering.Authority * steerer.BrakeFactor, MinBrake);
        var left = Math.Max(remaining - margin, 0f);

        // Near the end the speed is proportional to the distance: the root alone is infinitely steep there and
        // the agent would flip between full speed forward and back every tick.
        var speed = Math.Min(GetSpeed(modifier), Math.Min(MathF.Sqrt(2f * brake * left), left * steerer.ApproachGain));

        var toAim = aim - pos;
        var distance = toAim.Length();
        steering.Desired = distance > 0.01f ? toAim / distance * speed : Vector2.Zero;

        // The agent is not to run into anything, and it is not to crowd the others.
        steering.Desired = Avoid(uid, steering, steerer, mover, xform, velocity, previous, brake, frameTime);

        // Stuck: it is told to move and does not.
        if (steering.Desired.LengthSquared() < 0.25f || (pos - steering.StuckPosition).Length() > steerer.StuckDistance)
        {
            steering.StuckPosition = pos;
            steering.StuckSince = now;
        }
        else if (now - steering.StuckSince > steerer.StuckTime)
        {
            Finish(steering, RfSteeringStatus.Stuck);
        }
    }

    private static void Finish(RfSteeringComponent steering, RfSteeringStatus status)
    {
        steering.Status = status;
        steering.Desired = Vector2.Zero;
    }

    /// <summary>
    /// The input that brings the velocity to the desired one. It is not limited to a few directions or to full speed,
    /// the mover takes any vector no longer than one, and when the desired speed is lower than the current one the
    /// input points the other way, that brakes much quicker than waiting for the floor to do it.
    /// </summary>
    private static Vector2 GetInput(
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        MovementSpeedModifierComponent? modifier,
        Vector2 velocity,
        float frameTime)
    {
        if (steering.Desired == Vector2.Zero && velocity.Length() <= steerer.StopSpeed * 0.5f)
            return Vector2.Zero;

        // The mover gives the speed that is proportional to the input when nothing is in the way. What is left is
        // the difference of the speeds, a share of which is corrected every tick, as the input changes the speed
        // by the measured amount a tick, so that the correction never overshoots.
        var speed = GetSpeed(modifier);
        var perTick = Math.Max(steering.Authority * frameTime, MinStep);
        var input = steering.Desired / speed + (steering.Desired - velocity) / perTick * steerer.Gain;
        var length = input.Length();

        return length > 1f ? input / length : input;
    }

    /// <summary>
    /// Measures how fast the velocity of the agent changes with the input it was given on the last tick.
    /// </summary>
    private static void MeasureAuthority(
        RfSteeringComponent steering,
        MovementSpeedModifierComponent? modifier,
        Vector2 velocity,
        float frameTime)
    {
        var strength = steering.LastInput.Length();

        if (steering.Samples == 0)
            steering.Authority = modifier?.Acceleration ?? MovementSpeedModifierComponent.DefaultAcceleration;

        if (strength < 0.3f || frameTime <= 0f)
            return;

        var change = Vector2.Dot(velocity - steering.LastVelocity, steering.LastInput / strength) / frameTime / strength;

        // The change against the input, a collision, or a wall tell nothing about the floor.
        if (change <= 0f)
            return;

        // The first measures count more, there is nothing but a guess before them.
        var rate = Math.Max(1f / (steering.Samples + 1), AuthorityRate);
        steering.Authority += (Math.Min(change, MaxAuthority) - steering.Authority) * rate;
        steering.Samples++;
    }

    private static float GetSpeed(MovementSpeedModifierComponent? modifier)
        => modifier?.CurrentSprintSpeed is > 0f and var speed ? speed : DefaultSpeed;

    private void SetInput(InputMoverComponent mover, Vector2 input, GameTick tick)
    {
        mover.CurTickSprintMovement = input;
        mover.LastInputTick = tick;
        mover.LastInputSubTick = ushort.MaxValue;
    }

    /// <summary>
    /// Starts moving the agent along the path. The one that was followed is dropped.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="result">The path, only the nodes on the first grid of it are followed.</param>
    /// <param name="target">Where the path was searched for.</param>
    /// <param name="range">How close to the end of the path the agent needs to get.</param>
    /// <returns>False if the path is empty.</returns>
    [PublicAPI]
    public bool Follow(EntityUid agent, PathSearchResult result, EntityCoordinates target, float range)
    {
        if (result.Path is not { Count: > 0 } path)
            return false;

        var steering = EnsureComp<RfSteeringComponent>(agent);
        var graph = path[0].Graph;

        steering.Graph = graph;
        steering.Nodes.Clear();
        steering.Remaining.Clear();
        steering.Index = 0;
        steering.Range = Math.Max(range, MinRange);
        steering.Status = RfSteeringStatus.Moving;
        steering.Desired = Vector2.Zero;
        steering.NextDecision = _timing.CurTick;
        steering.StuckSince = _timing.CurTime;
        steering.StuckPosition = Transform(agent).LocalPosition;
        steering.LastInput = Vector2.Zero;
        steering.Samples = 0;
        steering.Radius = GetRadius(agent);

        foreach (var node in path)
        {
            if (node.Graph != graph)
                break;

            steering.Nodes.Add(node.Box);
        }

        var lastCenter = steering.Nodes[^1].Center;

        // The end is the target only if the path leads to it.
        steering.End = result.Status == PathSearchStatus.Path && steering.Nodes.Count == path.Count
            ? _transform.WithEntityId(target, graph).Position
            : lastCenter;

        // Distances from the end, counted back.
        var distance = (steering.End - lastCenter).Length();

        for (var i = steering.Nodes.Count - 1; i >= 0; i--)
        {
            if (i < steering.Nodes.Count - 1)
                distance += (steering.Nodes[i + 1].Center - steering.Nodes[i].Center).Length();

            steering.Remaining.Add(distance);
        }

        steering.Remaining.Reverse();

        var ev = new SpriteMoveEvent(true);
        RaiseLocalEvent(agent, ref ev);
        return true;
    }

    // The smallest distance the agent can be asked to get to the end, it cannot stop closer than that.
    private const float MinRange = 0.15f;

    /// <summary>
    /// Stops the movement of the agent. The agent brakes with the input and the component is removed when it has
    /// stopped, the floor decides how long that takes.
    /// </summary>
    [PublicAPI]
    public void Stop(EntityUid agent)
    {
        if (!TryComp(agent, out RfSteeringComponent? steering))
            return;

        Finish(steering, RfSteeringStatus.Stopping);
    }
}
