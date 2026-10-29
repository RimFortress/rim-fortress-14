using System.Numerics;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Server._RF.NPC.Steering.Systems;

// Not running into bodies and keeping away from the other agents.
public sealed partial class RfSteeringSystem
{
    // The directions tried instead of the desired one, in degrees to the right (negative) and to the left.
    // The right ones come first and are a bit cheaper, so two agents that meet go around each other on the same sides.
    // The small ones are for the narrow places, where a turn of twenty degrees is the other wall.
    private static readonly float[] Turns = [-5f, 5f, -10f, 10f, -15f, 15f, -20f, 20f, -40f, 40f, -65f, 65f, -90f, 90f, -130f, 130f];
    private static readonly float[] Scales = [1f, 0.5f];

    private const float LeftCost = 0.05f;
    private const float KeepCost = 0.6f;
    private const float CollisionCost = 1.5f;
    private const float SoonCost = 2f;

    // How much worse the side opposite to the one the agent is going around something by is, for a while.
    private const float SideCost = 1.5f;
    private static readonly TimeSpan SideTime = TimeSpan.FromSeconds(1.2);

    // How far from what does not move the agent keeps, besides its radius: the path is made along such things,
    // and the agent has to be able to follow it.
    private const float StaticClearance = 0f;

    // How close to a body the end of the path is for the body to be the one the path leads to.
    private const float EndMargin = 0.1f;


    // How much farther than the next node of the path the agent looks for what is in the way.
    private const float AimSlack = 0f;

    // The farthest ahead along the path the agent looks for what is in the way.
    private const float MaxReach = 12f;

    // The bodies near the agent, reused by all the agents.
    private readonly HashSet<EntityUid> _near = new();
    private readonly List<Obstacle> _obstacles = new();

    /// <summary>
    /// A body that can be in the way, the box and the velocity are in the world frame. If the body is round, which
    /// is what the trees are, <paramref name="Circle"/> is its radius and its center is the center of the box.
    /// </summary>
    private readonly record struct Obstacle(Box2 Box, Vector2 Velocity, bool Agent, float Circle);

    /// <summary>
    /// Takes the velocity the agent wants and returns the one that does not lead into anything in the time ahead
    /// and is the closest to it. Most of the time nothing is in the way and the wanted velocity is the result.
    /// </summary>
    private Vector2 Avoid(
        EntityUid uid,
        RfSteeringComponent steering,
        RfSteererComponent steerer,
        InputMoverComponent mover,
        TransformComponent xform,
        Vector2 velocity,
        Vector2 previous,
        Vector2 toAim,
        Vector2 toEnd,
        float brake,
        TimeSpan now,
        float frameTime)
    {
        var wanted = steering.Desired;

        if (wanted == Vector2.Zero)
            return wanted;

        // The agent moves in the frame of its grid, the bodies are looked for in the world one.
        var angle = _mover.GetParentGridAngle(mover);
        var wantedWorld = angle.RotateVec(wanted);
        var velocityWorld = angle.RotateVec(velocity);
        var position = _transform.GetWorldPosition(xform);
        var radius = steering.Radius + steerer.Clearance;

        // How long the agent needs to stop is how long it has to see ahead, and it decides again only after a while.
        var horizon = Math.Clamp(
            velocityWorld.Length() / Math.Max(brake, MinBrake) + steerer.UpdateInterval * frameTime + 0.5f,
            0.5f,
            steerer.AvoidHorizon);

        var reach = radius + wantedWorld.Length() * horizon + steerer.SeparationRange + 0.5f;
        Gather(uid, steering.Ignore, xform, position, reach);

        // What does not move is accounted for by the path. If the place it leads to is next to such a body, only too
        // close for the agent to keep its distance, the agent is meant to be there, and the physics keeps it from
        // getting into the body. The body that the path ends in is the one the agent is going to, such as a wall that
        // it is to break, and it is not avoided either.
        var aim = position + angle.RotateVec(toAim);
        var end = position + angle.RotateVec(toEnd);
        var body = steering.Radius + StaticClearance;
        _obstacles.RemoveAll(o => !o.Agent && (Inside(o, end, EndMargin) || Inside(o, aim, body) && !Inside(o, aim, 0f)));

        if (_obstacles.Count == 0)
        {
            steering.AvoidSide = 0;
            return wanted;
        }

        wantedWorld += Separate(steerer, steering.Radius, position, wantedWorld.Length());

        var best = wantedWorld;
        var previousWorld = angle.RotateVec(previous);
        var committed = now < steering.AvoidUntil ? steering.AvoidSide : 0;
        var reach2 = StraightReach(steering, position, toAim, angle) + AimSlack;
        var bestCost = Cost(wantedWorld, wantedWorld, previousWorld, position, steering.Radius, radius, horizon, reach2, out var hits);

        if (!hits)
        {
            // Nothing is in the way, the agent has gone around it.
            if (now >= steering.AvoidUntil)
                steering.AvoidSide = 0;

            return (-angle).RotateVec(best);
        }

        // A way that leads into nothing is better than any that does, however far it is from the wanted one.
        var side = 0;
        var bestHits = true;

        foreach (var scale in Scales)
        {
            foreach (var turn in Turns)
            {
                var candidate = new Angle(turn * MathF.PI / 180f).RotateVec(wantedWorld) * scale;
                var cost = Cost(candidate, wantedWorld, previousWorld, position, steering.Radius, radius, horizon, reach2, out hits)
                           + (turn > 0f ? LeftCost : 0f)
                           + (committed != 0 && Math.Sign(turn) == -committed ? SideCost : 0f);

                if (!IsBetter(cost, hits, bestCost, bestHits))
                    continue;

                best = candidate;
                bestCost = cost;
                bestHits = hits;
                side = Math.Sign(turn);
            }
        }

        // Nothing leads out: stand and let the others pass.
        if (bestHits)
        {
            var stand = Cost(Vector2.Zero, wantedWorld, previousWorld, position, steering.Radius, radius, horizon, reach2, out hits);

            if (IsBetter(stand, hits, bestCost, bestHits))
            {
                best = Vector2.Zero;
                side = 0;
            }
        }

        // The side is kept until the agent is past the body, or it would go to one side and then to the other.
        if (side != 0)
        {
            steering.AvoidSide = side;
            steering.AvoidUntil = now + SideTime;
        }

        return (-angle).RotateVec(best);
    }

    private static bool IsBetter(float cost, bool hits, float bestCost, bool bestHits)
        => hits == bestHits ? cost < bestCost : !hits;

    /// <summary>
    /// Whether the point is in the body, inflated by the radius.
    /// </summary>
    private static bool Inside(in Obstacle obstacle, Vector2 point, float radius)
        => obstacle.Circle > 0f
            ? Vector2.Distance(obstacle.Box.Center, point) < obstacle.Circle + radius
            : obstacle.Box.Enlarged(radius).Contains(point);

    /// <summary>
    /// How far ahead the way of the agent is straight: to the node it is heading for and on, as long as the path does
    /// not turn. What is farther than that is not in the way, the agent turns before it.
    /// </summary>
    private static float StraightReach(RfSteeringComponent steering, Vector2 position, Vector2 toAim, Angle frame)
    {
        var nodes = steering.Nodes;
        var reach = toAim.Length();

        for (var i = steering.Index + 1; i + 1 < nodes.Count && reach < MaxReach; i++)
        {
            var from = nodes[i].Center - nodes[i - 1].Center;
            var to = nodes[i + 1].Center - nodes[i].Center;

            if (Vector2.Dot(from, to) <= 0.95f * from.Length() * to.Length())
                break;

            reach += to.Length();
        }

        return reach;
    }

    /// <summary>
    /// Collects the bodies the agent can run into.
    /// </summary>
    private void Gather(EntityUid uid, EntityUid? ignore, TransformComponent xform, Vector2 position, float reach)
    {
        _obstacles.Clear();
        _near.Clear();

        _lookup.GetEntitiesIntersecting(
            xform.MapID,
            Box2.CenteredAround(position, new Vector2(reach * 2f, reach * 2f)),
            _near,
            LookupFlags.Static | LookupFlags.Dynamic);

        if (!_physicsQuery.TryComp(uid, out var body)
            || !_fixturesQuery.TryComp(uid, out var fixtures))
            return;

        foreach (var other in _near)
        {
            if (other == uid
                || other == ignore
                || !_physicsQuery.TryComp(other, out var otherBody)
                || !otherBody.CanCollide
                || !_physics.IsCurrentlyHardCollidable((uid, fixtures, body), other))
                continue;

            var agent = otherBody.BodyType != BodyType.Static;
            _obstacles.Add(new Obstacle(
                _lookup.GetWorldAABB(other),
                agent ? otherBody.LinearVelocity : Vector2.Zero,
                agent,
                GetCircle(other)));
        }
    }

    /// <summary>
    /// The velocity away from the agents that are close, it grows as they get closer.
    /// </summary>
    private Vector2 Separate(RfSteererComponent steerer, float radius, Vector2 position, float speed)
    {
        var push = Vector2.Zero;

        foreach (var obstacle in _obstacles)
        {
            if (!obstacle.Agent)
                continue;

            var away = position - obstacle.Box.Center;
            var distance = away.Length();

            // The size of the other one is taken from its box, it is a circle for a mob.
            var limit = radius + Math.Max(obstacle.Box.Width, obstacle.Box.Height) * 0.5f + steerer.SeparationRange;

            if (distance >= limit || distance < 0.001f)
                continue;

            push += away / distance * (1f - distance / limit) * steerer.SeparationSpeed;
        }

        var length = push.Length();

        // It does not outweigh the way to the target.
        return length > speed * 0.5f ? push / length * speed * 0.5f : push;
    }

    /// <summary>
    /// How bad a velocity is: how far it is from the wanted one and from the one chosen before, and, a lot worse,
    /// whether it leads into something, the sooner the worse.
    /// </summary>
    private float Cost(
        Vector2 candidate,
        Vector2 wanted,
        Vector2 previous,
        Vector2 position,
        float bodyRadius,
        float radius,
        float horizon,
        float reach,
        out bool hit)
    {
        var scale = Math.Max(wanted.Length(), 0.01f);
        var cost = (candidate - wanted).Length() / scale + KeepCost * (candidate - previous).Length() / scale;
        var soonest = horizon;

        foreach (var obstacle in _obstacles)
        {
            // Only what moves is kept away from by a margin, the path is made along the rest.
            var margin = obstacle.Agent ? radius : bodyRadius + StaticClearance;
            var relative = candidate - obstacle.Velocity;
            var time = obstacle.Circle > 0f
                ? TimeToHitCircle(position, relative, obstacle.Box.Center, obstacle.Circle + margin)
                : TimeToHit(position, relative, obstacle.Box.Enlarged(margin), obstacle.Box.Center);

            // What is farther than the node the agent is heading for does not matter: the way turns there, and the
            // agent decides again. Without it, the wall at the end of a corridor is a collision all the way along it.
            if (time * candidate.Length() > reach)
                continue;

            if (time < soonest)
                soonest = time;
        }

        hit = soonest < horizon;

        if (hit)
            cost += CollisionCost + SoonCost * (horizon - soonest) / horizon;

        return cost;
    }

    /// <summary>
    /// When a point moving with the velocity gets into the circle, infinity if it does not. A point that is inside
    /// already hits it only if it moves toward the center.
    /// </summary>
    private static float TimeToHitCircle(Vector2 point, Vector2 velocity, Vector2 center, float radius)
    {
        var offset = point - center;
        var c = offset.LengthSquared() - radius * radius;

        // Inside already, it is going deeper only if it moves toward the center: along the surface is not a collision,
        // an agent that is a little too close to a body, as it is when it stands against it, must be able to go on.
        if (c <= 0f)
            return Vector2.Dot(velocity, offset) < 0f ? 0f : float.PositiveInfinity;

        var a = velocity.LengthSquared();
        var b = Vector2.Dot(offset, velocity);

        if (a < 0.0001f || b >= 0f)
            return float.PositiveInfinity;

        var discriminant = b * b - a * c;
        return discriminant < 0f ? float.PositiveInfinity : (-b - MathF.Sqrt(discriminant)) / a;
    }

    /// <summary>
    /// When a point moving with the velocity gets into the box, infinity if it does not within the time asked.
    /// A point that is inside already hits it only if it moves toward the center.
    /// </summary>
    private static float TimeToHit(Vector2 point, Vector2 velocity, Box2 box, Vector2 center)
    {
        // Inside already, the way out is the nearest side, and the agent is going deeper only if it moves toward it from
        // outside. Moving along the side is not a collision: an agent that is a little too close to a wall, as it is
        // when it stands against it, must be able to go on along it.
        if (box.Contains(point))
        {
            var left = point.X - box.Left;
            var right = box.Right - point.X;
            var bottom = point.Y - box.Bottom;
            var top = box.Top - point.Y;
            var nearest = Math.Min(Math.Min(left, right), Math.Min(bottom, top));
            var normal = nearest == left
                ? new Vector2(-1f, 0f)
                : nearest == right
                    ? new Vector2(1f, 0f)
                    : nearest == bottom
                        ? new Vector2(0f, -1f)
                        : new Vector2(0f, 1f);

            return Vector2.Dot(velocity, normal) < 0f ? 0f : float.PositiveInfinity;
        }

        var enter = 0f;
        var exit = float.PositiveInfinity;

        if (!Slab(point.X, velocity.X, box.Left, box.Right, ref enter, ref exit)
            || !Slab(point.Y, velocity.Y, box.Bottom, box.Top, ref enter, ref exit))
        {
            return float.PositiveInfinity;
        }

        return enter <= exit ? enter : float.PositiveInfinity;
    }

    private static bool Slab(float point, float velocity, float min, float max, ref float enter, ref float exit)
    {
        if (Math.Abs(velocity) < 0.0001f)
            return point >= min && point <= max;

        var a = (min - point) / velocity;
        var b = (max - point) / velocity;

        if (a > b)
            (a, b) = (b, a);

        enter = Math.Max(enter, a);
        exit = Math.Min(exit, b);
        return true;
    }

    /// <summary>
    /// Returns the radius of the body if it is one circle, and zero otherwise.
    /// </summary>
    private float GetCircle(EntityUid uid)
    {
        if (!_fixturesQuery.TryComp(uid, out var fixtures))
            return 0f;

        var radius = 0f;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            // Anything but a single circle is taken as the box it fits in.
            if (radius > 0f || fixture.Shape is not PhysShapeCircle circle)
                return 0f;

            radius = circle.Radius;
        }

        return radius;
    }

    private float GetRadius(EntityUid uid)
    {
        var radius = 0f;

        if (!_fixturesQuery.TryComp(uid, out var fixtures))
            return radius > 0f ? radius : DefaultRadius;

        foreach (var fixture in fixtures.Fixtures.Values)
        {
            if (!fixture.Hard)
                continue;

            var box = fixture.Shape.ComputeAABB(new PhysicsTransform(Vector2.Zero, Angle.Zero), 0);
            radius = Math.Max(radius, Math.Max(box.Width, box.Height) * 0.5f);
        }

        return radius > 0f ? radius : DefaultRadius;
    }

    private const float DefaultRadius = 0.35f;
}
