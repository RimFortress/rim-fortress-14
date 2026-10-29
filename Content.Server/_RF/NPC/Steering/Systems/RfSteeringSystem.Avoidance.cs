using System.Numerics;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared.Movement.Components;
using Robust.Shared.Physics;
using PhysicsTransform = Robust.Shared.Physics.Transform;

namespace Content.Server._RF.NPC.Steering.Systems;

// Not running into bodies and keeping away from the other agents.
public sealed partial class RfSteeringSystem
{
    // The directions tried instead of the desired one, in degrees to the right (negative) and to the left.
    // The right ones come first and are a bit cheaper, so two agents that meet go around each other on the same sides.
    private static readonly float[] Turns = [-20f, 20f, -40f, 40f, -65f, 65f, -90f, 90f, -130f, 130f];
    private static readonly float[] Scales = [1f, 0.5f];

    private const float LeftCost = 0.05f;
    private const float KeepCost = 0.6f;
    private const float CollisionCost = 1.5f;
    private const float SoonCost = 2f;

    // The bodies near the agent, reused by all the agents.
    private readonly HashSet<EntityUid> _near = new();
    private readonly List<Obstacle> _obstacles = new();

    /// <summary>
    /// A body that can be in the way, the box and the velocity are in the world frame.
    /// </summary>
    private readonly record struct Obstacle(Box2 Box, Vector2 Velocity, bool Agent);

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
        float brake,
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
        Gather(uid, xform, position, reach);

        if (_obstacles.Count == 0)
            return wanted;

        wantedWorld += Separate(steerer, steering.Radius, position, wantedWorld.Length());

        var best = wantedWorld;
        var bestCost = Cost(wantedWorld, wantedWorld, angle.RotateVec(previous), position, radius, horizon);

        if (bestCost < CollisionCost)
            return (-angle).RotateVec(best);

        foreach (var scale in Scales)
        {
            foreach (var turn in Turns)
            {
                var candidate = new Angle(turn * MathF.PI / 180f).RotateVec(wantedWorld) * scale;
                var cost = Cost(candidate, wantedWorld, angle.RotateVec(previous), position, radius, horizon)
                           + (turn > 0f ? LeftCost : 0f);

                if (cost >= bestCost)
                    continue;

                best = candidate;
                bestCost = cost;
            }
        }

        // Nothing leads out: stand and let the others pass.
        var stand = Cost(Vector2.Zero, wantedWorld, angle.RotateVec(previous), position, radius, horizon);

        if (bestCost >= CollisionCost && stand < bestCost)
        {
            best = Vector2.Zero;
            bestCost = stand;
        }

        return (-angle).RotateVec(best);
    }

    /// <summary>
    /// Collects the bodies the agent can run into.
    /// </summary>
    private void Gather(EntityUid uid, TransformComponent xform, Vector2 position, float reach)
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
                || !_physicsQuery.TryComp(other, out var otherBody)
                || !otherBody.CanCollide
                || !_physics.IsCurrentlyHardCollidable((uid, fixtures, body), other))
                continue;

            var agent = otherBody.BodyType != BodyType.Static;
            _obstacles.Add(new Obstacle(
                _lookup.GetWorldAABB(other),
                agent ? otherBody.LinearVelocity : Vector2.Zero,
                agent));
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
        float radius,
        float horizon)
    {
        var scale = Math.Max(wanted.Length(), 0.01f);
        var cost = (candidate - wanted).Length() / scale + KeepCost * (candidate - previous).Length() / scale;
        var soonest = horizon;

        foreach (var obstacle in _obstacles)
        {
            var time = TimeToHit(position, candidate - obstacle.Velocity, obstacle.Box.Enlarged(radius), obstacle.Box.Center);

            if (time < soonest)
                soonest = time;
        }

        if (soonest < horizon)
            cost += CollisionCost + SoonCost * (horizon - soonest) / horizon;

        return cost;
    }

    /// <summary>
    /// When a point moving with the velocity gets into the box, infinity if it does not within the time asked.
    /// A point that is inside already hits it only if it moves toward the center.
    /// </summary>
    private static float TimeToHit(Vector2 point, Vector2 velocity, Box2 box, Vector2 center)
    {
        if (box.Contains(point))
            return Vector2.Dot(velocity, center - point) > 0f ? 0f : float.PositiveInfinity;

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
