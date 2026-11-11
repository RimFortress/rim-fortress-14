using Content.Shared.EntityConditions;
using System.Numerics;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.Executable.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared.CombatMode;
using Content.Shared.Interaction;
using Content.Shared.Maps;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
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
    [Dependency] private SharedExecutableGoalSystem _executable = null!;
    [Dependency] private SharedMapSystem _maps = null!;
    [Dependency] private ITileDefinitionManager _tileDefs = null!;
    [Dependency] private SharedInteractionSystem _interaction = null!;
    [Dependency] private SharedPhysicsSystem _physics = null!;
    [Dependency] private SharedCombatModeSystem _combatMode = null!;

    [Dependency] private EntityQuery<MapGridComponent> _gridQuery;
    [Dependency] private EntityQuery<PhysicsComponent> _physicsQuery;
    [Dependency] private SharedEntityConditionsSystem _conditions = null!;
    [Dependency] private EntityQuery<FixturesComponent> _fixturesQuery;
    [Dependency] private EntityQuery<MovementSpeedModifierComponent> _modifierQuery;

    // If the agent has no modifier, it moves like a mob without any.
    private const float DefaultSprintSpeed = MovementSpeedModifierComponent.DefaultBaseSprintSpeed;
    private const float DefaultWalkSpeed = MovementSpeedModifierComponent.DefaultBaseWalkSpeed;
    private const float MinBrake = 1f;

    // The share of the full speed at which a right-angle turn is taken, the turn is slower the sharper it is.
    private const float CornerSpeed = 0.3f;

    // How far short of the line across the way through a node the agent has reached the node, in meters.
    private const float NodeReach = 0.12f;

    // The least the input changes the speed in a tick, in meters a second, and the most that is believed.
    private const float MinStep = 0.05f;

    // How much faster than it wants the agent can be before it brakes, in meters a second.
    private const float OverSpeed = 0.2f;
    private const float MaxAuthority = 400f;

    // The least change of the speed by the input, in meters a second a second, that is believed to be the floor.
    private const float MinSample = 1.5f;

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
        if (!TerminatingOrDeleted(ent))
            _executable.ClearRoute(ent);

        if (TerminatingOrDeleted(ent) || !TryComp(ent, out InputMoverComponent? mover))
            return;

        mover.CurTickSprintMovement = Vector2.Zero;
        mover.CurTickWalkMovement = Vector2.Zero;

        if (ent.Comp.AddedNoRotate)
            RemComp<NoRotateOnMoveComponent>(ent);

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

            SetInput(mover, Vector2.Zero, tick, steering.Sprint);
            return;
        }

        _modifierQuery.TryComp(uid, out var modifier);

        // The agent walks, and runs when the order says so, or when it is in the combat mode and the order has nothing to say about it.
        steering.Sprint = steerer.Sprint ?? _combatMode.IsInCombatMode(uid);

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

        if (steering.AddedNoRotate && steering.Status == RfSteeringStatus.Moving)
            Face(uid, steering, steerer, xform, mover, velocity, frameTime);

        var input = GetInput(steering, steerer, modifier, velocity, frameTime);

        steering.LastVelocity = velocity;
        steering.LastInput = input;
        SetInput(mover, input, tick, steering.Sprint);
    }

    /// <summary>
    /// Turns the agent to where it goes, and not to where the input points: when it brakes with the input the two
    /// are opposite, and an agent would come to its target backwards.
    /// </summary>
    private void Face(
        EntityUid uid,
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        TransformComponent xform,
        InputMoverComponent mover,
        Vector2 velocity,
        float frameTime)
    {
        Angle target;

        if (steering.FaceEntity is { } face && !TerminatingOrDeleted(face)
            && _transform.GetWorldPosition(face) - _transform.GetWorldPosition(uid) is var toFace
            && toFace.LengthSquared() > 0.01f)
            target = toFace.ToWorldAngle();
        else
        {
            // What the agent wants, and where it is going if it wants nothing yet or is sliding.
            var heading = steering.Desired.LengthSquared() > 0.04f
                ? steering.Desired
                : velocity.LengthSquared() > 0.25f
                    ? velocity
                    : Vector2.Zero;

            if (heading == Vector2.Zero)
                return;

            target = _mover.GetParentGridAngle(mover).RotateVec(heading).ToWorldAngle();
        }

        var current = _transform.GetWorldRotation(xform);
        var difference = Angle.ShortestDistance(current, target);
        var step = Angle.FromDegrees(steerer.TurnSpeed * frameTime);

        if (Math.Abs(difference.Theta) < 0.001)
            return;

        _transform.SetWorldRotation(uid, current + Math.Clamp(difference.Theta, -step.Theta, step.Theta));
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

        AdjustToFloor(steering, modifier, xform);
        var last = nodes.Count - 1;

        // The agent moves on to the next node when it has passed it, which it has if it is past the line across the way
        // through the node. A node is not passed on getting into its tile: the corner of a corridor is turned at, and
        // not cut. Going around something takes the agent off the nodes, and being pushed ahead takes it past several.
        while (steering.Index < last)
        {
            var from = nodes[steering.Index].Center;
            var next = nodes[steering.Index + 1].Center;

            var way = next - from;
            var length = way.Length();

            // A node is reached a little short of its line too, the agent slows down for the turn and may stop there.
            if (length > 0.0001f && Vector2.Dot(pos - next, way) / length < -NodeReach)
                break;

            steering.Index++;
        }

        // The nodes of straight parts of the path are left out, so it is the distance to the path that tells that the
        // agent has left it, and not the distance to its node.
        if (DistanceToPath(steering, pos) > steerer.LeaveDistance)
        {
            Finish(steering, RfSteeringStatus.OffPath);
            return;
        }

        var toEnd = steering.Bounds is { } region ? DistanceTo(region, pos) : (steering.End - pos).Length();

        // Seeing the target for a while is enough, the agent need not come up to it.
        if (steering.Sight is { } sight)
        {
            if (_interaction.InRangeUnobstructed(uid, sight, SightRange))
                steering.SightSeen += Math.Max(steerer.UpdateInterval, 1) * frameTime;
            else
                steering.SightSeen = 0f;

            if (steering.SightSeen >= SightTime)
            {
                Finish(steering, RfSteeringStatus.InRange);
                return;
            }
        }

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
        var approach = steerer.ApproachGain / (Math.Max(steerer.UpdateInterval, 1) * Math.Max(frameTime, 0.001f));
        var speed = Math.Min(GetSpeed(modifier, steering.Sprint), Math.Min(MathF.Sqrt(2f * brake * left), left * approach));

        var toAim = aim - pos;
        var distance = toAim.Length();

        // The speed from which the agent can take the turn at the node it is heading for.
        if (steering.Index + 2 <= last && distance > 0.01f)
        {
            var after = nodes[steering.Index + 2].Center - aim;
            var cosine = after.LengthSquared() > 0.0001f ? Vector2.Dot(toAim / distance, after.Normalized()) : 1f;

            if (cosine < 0.95f)
            {
                var turn = GetSpeed(modifier, steering.Sprint) * (CornerSpeed + (1f - CornerSpeed) * Math.Max(cosine, 0f));
                speed = Math.Min(speed, MathF.Sqrt(turn * turn + 2f * brake * distance));
            }
        }
        steering.Desired = distance > 0.01f ? toAim / distance * speed : Vector2.Zero;

        // Whether the agent gets anywhere is judged by where it wants to go and not by where it is allowed to: an agent
        // that waits for the others to make way for it is as stuck as the one that is pressed against a wall.
        var wanted = steering.Desired.LengthSquared();

        // The agent is not to run into anything, and it is not to crowd the others.
        steering.Desired = Avoid(uid, steering, steerer, mover, xform, velocity, previous, toAim, steering.End - pos, brake, now, frameTime);

        // Stuck: it is told to move and does not.
        if (wanted < 0.25f || (pos - steering.StuckPosition).Length() > steerer.StuckDistance)
        {
            steering.StuckPosition = pos;
            steering.StuckSince = now;
        }
        else if (now - steering.StuckSince > steerer.StuckTime)
        {
            Finish(steering, RfSteeringStatus.Stuck);
        }

        // Moving is not getting anywhere: an agent can go to and fro in front of what it cannot get by.
        if (remaining < steering.BestRemaining - steerer.ProgressDistance)
        {
            steering.BestRemaining = remaining;
            steering.ProgressSince = now;
        }
        else if (wanted >= 0.25f && now - steering.ProgressSince > steerer.NoProgressTime)
        {
            Finish(steering, RfSteeringStatus.Stuck);
        }
    }

    /// <summary>
    /// The measured response of the agent to the input is of the floor it was measured on. The agent cannot measure
    /// it again while it keeps going at one speed with one input, so when the floor changes the measure is changed by
    /// as much as the acceleration of the floor, that the mover applies, has changed.
    /// </summary>
    private void AdjustToFloor(RfSteeringComponent steering, MovementSpeedModifierComponent? modifier, TransformComponent xform)
    {
        var floor = 1f;

        if (_gridQuery.TryComp(xform.GridUid, out var grid)
            && _maps.TryGetTileRef(xform.GridUid.Value, grid, xform.Coordinates, out var tile)
            && _tileDefs[tile.Tile.TypeId] is ContentTileDefinition definition)
            floor = definition.MobAcceleration ?? 1f;

        if (steering.Samples == 0)
            steering.Authority = (modifier?.Acceleration ?? MovementSpeedModifierComponent.DefaultAcceleration) * floor;
        else if (steering.Floor > 0f && !MathHelper.CloseTo(steering.Floor, floor))
            steering.Authority *= floor / steering.Floor;

        steering.Floor = floor;
    }

    private static float DistanceToPath(RfSteeringComponent steering, Vector2 pos)
    {
        var nodes = steering.Nodes;
        var distance = (nodes[steering.Index].Center - pos).Length();

        // The segments on both sides of the node the agent is at. The nodes are the centers of the polys, and a poly can be large: the path
        // begins where the agent was and ends at the end of the path, and not at the centers of the first and the last poly, or an agent
        // that walks straight to the target inside the poly would be taken for having left the path.
        if (steering.Index > 0)
            distance = Math.Min(distance, DistanceToSegment(nodes[steering.Index - 1].Center, nodes[steering.Index].Center, pos));
        else
            distance = Math.Min(distance, DistanceToSegment(steering.Start, nodes[0].Center, pos));

        if (steering.Index < nodes.Count - 1)
            distance = Math.Min(distance, DistanceToSegment(nodes[steering.Index].Center, nodes[steering.Index + 1].Center, pos));
        else
            distance = Math.Min(distance, DistanceToSegment(nodes[steering.Index].Center, steering.End, pos));

        if (nodes.Count == 1)
            distance = Math.Min(distance, DistanceToSegment(steering.Start, steering.End, pos));

        return distance;
    }

    private static float DistanceToSegment(Vector2 a, Vector2 b, Vector2 point)
    {
        var way = b - a;
        var length = way.LengthSquared();
        var t = length > 0f ? Math.Clamp(Vector2.Dot(point - a, way) / length, 0f, 1f) : 0f;
        return (a + way * t - point).Length();
    }

    private static float DistanceTo(in Box2 box, Vector2 point)
    {
        var dx = Math.Max(Math.Max(box.Left - point.X, 0f), point.X - box.Right);
        var dy = Math.Max(Math.Max(box.Bottom - point.Y, 0f), point.Y - box.Top);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static void Finish(RfSteeringComponent steering, RfSteeringStatus status)
    {
        steering.Status = status;
        steering.Desired = Vector2.Zero;
    }

    /// <summary>
    /// The input that brings the velocity to the desired one. It is not limited to a few directions or to full speed,
    /// the mover takes any vector no longer than one. The mover speeds up toward the direction of the input by itself,
    /// so the input is the desired velocity, and it is corrected only where the mover does not do it by itself: when
    /// the agent is faster than it wants to be the input points the other way, that brakes much quicker than
    /// waiting for the floor to do it, and when it slides to the side the input takes it back.
    /// </summary>
    /// <remarks>
    /// The mover turns the velocity, and does not add the correction to it, so the response to the input is not
    /// the same along the velocity and across it. A correction in proportion to the difference of the velocities
    /// that is the same in both directions makes the agent twitch from side to side.
    /// </remarks>
    private static Vector2 GetInput(
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        MovementSpeedModifierComponent? modifier,
        Vector2 velocity,
        float frameTime)
    {
        var perTick = Math.Max(steering.Authority * frameTime, MinStep);
        Vector2 input;

        if (steering.Desired == Vector2.Zero)
        {
            if (velocity.Length() <= steerer.StopSpeed * 0.5f)
                return Vector2.Zero;

            input = -velocity / perTick * steerer.Gain;
        }
        else
        {
            var length = steering.Desired.Length();
            var direction = steering.Desired / length;
            var along = Vector2.Dot(velocity, direction);
            var across = velocity - direction * along;

            input = steering.Desired / GetSpeed(modifier, steering.Sprint);

            if (along - length > OverSpeed)
                input -= direction * ((along - length) / perTick * steerer.Gain);

            input -= across / perTick * steerer.SideGain;
        }

        var size = input.Length();
        return size > 1f ? input / size : input;
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

        if (strength < 0.3f || frameTime <= 0f)
            return;

        var change = Vector2.Dot(velocity - steering.LastVelocity, steering.LastInput / strength) / frameTime / strength;

        // The change against the input, a collision, or a wall tell nothing about the floor. An agent that is pressed
        // against something would otherwise "learn" that the input does nothing, and then give the strongest one.
        if (change < MinSample)
            return;

        // The first measures count more, there is nothing but a guess before them.
        var rate = Math.Max(1f / (steering.Samples + 1), AuthorityRate);
        steering.Authority += (Math.Min(change, MaxAuthority) - steering.Authority) * rate;
        steering.Samples++;
    }

    private static float GetSpeed(MovementSpeedModifierComponent? modifier, bool sprint) => sprint
        ? modifier?.CurrentSprintSpeed is > 0f and var run ? run : DefaultSprintSpeed
        : modifier?.CurrentWalkSpeed is > 0f and var walk
            ? walk
            : DefaultWalkSpeed;

    // The input of a walk and the input of a run are different ones for the mover, each is given a speed of its own.
    private static void SetInput(InputMoverComponent mover, Vector2 input, GameTick tick, bool sprint)
    {
        mover.CurTickSprintMovement = sprint ? input : Vector2.Zero;
        mover.CurTickWalkMovement = sprint ? Vector2.Zero : input;
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
    /// <param name="bounds">
    /// The region of the body the path leads to, in the coordinates of the grid. The range is counted from it, and the
    /// end of the path is the place on it that is the closest to the path.
    /// </param>
    /// <param name="ignore">The body the path leads to, it is not avoided.</param>
    /// <returns>False if the path is empty.</returns>
    [PublicAPI]
    public bool Follow(
        EntityUid agent,
        PathSearchResult result,
        EntityCoordinates target,
        float range,
        Box2? bounds = null,
        EntityUid? ignore = null)
    {
        if (result.Path is not { Count: > 0 } path)
            return false;

        var steering = EnsureComp<RfSteeringComponent>(agent);
        var graph = path[0].Graph;

        steering.Graph = graph;
        steering.Nodes.Clear();
        steering.Remaining.Clear();
        steering.Index = 0;
        // A mob in combat mode keeps its rotation to the one who commands it, the steering does not touch that.
        if (!HasComp<NoRotateOnMoveComponent>(agent))
        {
            AddComp<NoRotateOnMoveComponent>(agent);
            steering.AddedNoRotate = true;
        }

        steering.Bounds = bounds;
        steering.Ignore = ignore;
        steering.Sight = null;
        steering.FaceEntity = null;
        steering.SightSeen = 0f;
        steering.Radius = GetRadius(agent);
        steering.Range = Math.Max(range, MinRange);

        // The agent cannot get closer to a solid body than its size.
        if (bounds != null)
            steering.Range = Math.Max(steering.Range, steering.Radius + RangeMargin);
        steering.Status = RfSteeringStatus.Moving;
        steering.Desired = Vector2.Zero;
        steering.NextDecision = _timing.CurTick;
        steering.StuckSince = _timing.CurTime;
        steering.BestRemaining = float.MaxValue;
        steering.ProgressSince = _timing.CurTime;
        steering.StuckPosition = Transform(agent).LocalPosition;
        steering.Start = steering.StuckPosition;
        steering.LastInput = Vector2.Zero;
        steering.Samples = 0;

        var points = new List<Vector2>(path.Count);

        foreach (var node in path)
        {
            if (node.Graph != graph)
                break;

            points.Add(node.Box.Center);
        }

        var wholePath = points.Count == path.Count;

        // The path is made of the steps of a grid, the agent is not to walk by it as by rails.
        points = Smooth(agent, graph, points, steering);

        foreach (var point in points)
        {
            steering.Nodes.Add(Box2.CenteredAround(point, Vector2.Zero));
        }

        var lastCenter = steering.Nodes[^1].Center;

        // The end is the target only if the path leads to it. A body is come up to from the side of the path.
        steering.End = result.Status == PathSearchStatus.Path && wholePath
            ? bounds is { } region
                ? Vector2.Clamp(lastCenter, region.BottomLeft, region.TopRight)
                : _transform.WithEntityId(target, graph).Position
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

        // The player sees the way that the agent goes, and the clients are told only when it is another way.
        var route = new List<Vector2>(steering.Nodes.Count + 1);

        foreach (var node in steering.Nodes)
        {
            route.Add(node.Center);
        }

        if (Vector2.DistanceSquared(route[^1], steering.End) > 0.01f)
            route.Add(steering.End);

        _executable.SetRoute(agent, graph, route);

        var ev = new SpriteMoveEvent(true);
        RaiseLocalEvent(agent, ref ev);
        return true;
    }

    // The smallest distance the agent can be asked to get to the end, it cannot stop closer than that.
    private const float MinRange = 0.15f;

    // How far from a solid body the center of the agent can be said to be at it, besides the radius of the agent.
    private const float RangeMargin = 0.25f;

    // How far the agent looks for the target when it is enough to see it.
    private const float SightRange = 10f;

    // For how long the target must be in sight.
    private const float SightTime = 0.5f;

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
