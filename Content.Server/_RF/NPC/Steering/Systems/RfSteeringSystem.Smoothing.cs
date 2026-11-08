using System.Numerics;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.Pathfinding;
using Robust.Shared.Map;
using Robust.Shared.Physics;

namespace Content.Server._RF.NPC.Steering.Systems;

// Straightening the path: the pathfinding makes it of the steps of a grid, and an agent that follows it turns at right angles.
public sealed partial class RfSteeringSystem
{
    // How many nodes ahead a shortcut is looked for, it keeps the cost of straightening a long path in check.
    private const int MaxShortcut = 24;

    // How far apart the places are that are looked at on the way of a shortcut to see what the ground is like.
    private const float ShortcutStep = 0.25f;

    // How close to a body the way of a shortcut can be, by the influence of the bodies, see the tag of it. A shortcut is
    // for the open ground: where the agent is among the bodies it goes by the path, and does not cut the corners.
    private const float ShortcutInfluence = 0.2f;

    // How far from a body a shortcut passes, besides the radius of the agent. It is more than the agent keeps by when it
    // goes around, so that the agent has room to turn.
    private const float ShortcutClearance = 0.12f;

    private readonly List<Obstacle> _pathObstacles = new();

    /// <summary>
    /// Makes the path straighter: a node is left out if the agent can go from the one before it to the one after it in
    /// a straight line, and that line is as good as the way that was found. The agent can be given room to pass
    /// by the bodies that do not move, and the ground along the line has to be of the kind the pathfinding does not
    /// have anything against: it is not the ground that costs more, such as lava, and it is not a void.
    /// </summary>
    /// <returns>The points of the path in the coordinates of the grid, the first and the last stay.</returns>
    private List<Vector2> Smooth(EntityUid agent, EntityUid graph, List<Vector2> points, RfSteeringComponent steering)
    {
        if (points.Count < 3
            || TryComp(agent, out RfSteererComponent? steerer) && steerer.NoSmoothing
            || !_physicsQuery.TryComp(agent, out var body)
            || !_fixturesQuery.TryComp(agent, out var fixtures))
            return points;

        // The agent starts from where it is and not from the center of its tile.
        if (Transform(agent).ParentUid == graph)
            points[0] = Transform(agent).LocalPosition;

        var margin = steering.Radius + ShortcutClearance;
        GatherPathObstacles(agent, graph, points, margin, steering.Ignore, body, fixtures);
        _pathfinding.TryGetTagIndex(RfPathfindingSystem.ObstacleTag, out var obstacleTag);

        var result = new List<Vector2> { points[0] };
        var from = 0;

        while (from < points.Count - 1)
        {
            var to = Math.Min(points.Count - 1, from + MaxShortcut);

            while (to > from + 1 && !IsStraight(graph, points[from], points[to], margin, obstacleTag))
            {
                to--;
            }

            result.Add(points[to]);
            from = to;
        }

        return result;
    }

    private void GatherPathObstacles(
        EntityUid agent,
        EntityUid graph,
        List<Vector2> points,
        float margin,
        EntityUid? ignore,
        Robust.Shared.Physics.Components.PhysicsComponent body,
        FixturesComponent fixtures)
    {
        _pathObstacles.Clear();
        _near.Clear();

        var min = points[0];
        var max = points[0];

        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }

        var area = new Box2(min, max).Enlarged(margin + 1f);
        var mapId = Transform(graph).MapID;

        _lookup.GetEntitiesIntersecting(
            mapId,
            _transform.GetWorldMatrix(graph).TransformBox(area),
            _near,
            LookupFlags.Static);

        foreach (var other in _near)
        {
            if (other == agent
                || other == ignore
                || !_physicsQuery.TryComp(other, out var otherBody)
                || !otherBody.CanCollide
                || !_physics.IsCurrentlyHardCollidable((agent, fixtures, body), other))
                continue;

            // The bodies are kept in the coordinates of the grid, as the path is.
            var box = _transform.GetInvWorldMatrix(graph).TransformBox(_lookup.GetWorldAABB(other));
            _pathObstacles.Add(new Obstacle(box, Vector2.Zero, false, GetCircle(other)));
        }
    }

    /// <summary>
    /// Whether the agent can go from one point to the other in a straight line.
    /// </summary>
    private bool IsStraight(EntityUid graph, Vector2 from, Vector2 to, float margin, int obstacleTag, bool plain = true)
    {
        var way = to - from;

        foreach (var obstacle in _pathObstacles)
        {
            var time = obstacle.Circle > 0f
                ? TimeToHitCircle(from, way, obstacle.Box.Center, obstacle.Circle + margin)
                : TimeToHit(from, way, obstacle.Box.Enlarged(margin), obstacle.Box.Center);

            if (time <= 1f)
                return false;
        }

        // The ground along the line, the pathfinding has an opinion on it.
        var length = way.Length();
        var steps = (int) MathF.Ceiling(length / ShortcutStep);

        for (var i = 1; i < steps; i++)
        {
            var point = from + way * (i / (float) steps);

            if (_pathfinding.GetPoly(new EntityCoordinates(graph, point)) is not { } poly)
                return false;

            // A short step is not planned around the tags: it goes right next to the enemy, where they all are
            if (!plain)
            {
                if (!IsFloor(poly.Data))
                    return false;

                continue;
            }

            if (!IsPlain(poly.Data, obstacleTag)
                || _pathfinding.GetInfluence(poly.Data.Facts, RfPathfindingSystem.ObstacleTag) > ShortcutInfluence)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Whether the ground is the kind that a shortcut can go over: not a void, nothing that is to be destroyed, and no
    /// tag that makes the node cost more but the one of the closeness of the bodies, which the shortcut deals with itself.
    /// </summary>
    private static bool IsFloor(in RfPathfindingData data)
        => (data.Flags & RfPathfindingFlags.Space) == 0 && data.Facts.Destroy == null;

    private static bool IsPlain(in RfPathfindingData data, int obstacleTag)
    {
        if ((data.Flags & RfPathfindingFlags.Space) != 0 || data.Facts.Destroy != null)
            return false;

        for (var i = 0; i < data.Facts.InfluenceCount; i++)
        {
            if (data.Facts.Influence[i].Tag != obstacleTag)
                return false;
        }

        return true;
    }
}
