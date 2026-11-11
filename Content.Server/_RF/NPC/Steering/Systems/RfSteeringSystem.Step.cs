using System.Numerics;
using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.Pathfinding;
using JetBrains.Annotations;
using Robust.Shared.Map;

namespace Content.Server._RF.NPC.Steering.Systems;

// A step: a short walk to a place close by, with no path searched for.
// It is for the movements of a fighter that are too short and
// too frequent for the pathfinding (a step back, a step around the enemy),
// and it checks only that the way is clear.
public sealed partial class RfSteeringSystem
{
    /// <summary>
    /// Sends the agent to a place close by in a straight line, if the way there is clear:
    /// the ground is a plain floor and there are no
    /// solid bodies in the way (the bodies that move are avoided on the way, as always).
    /// The agent is not asked to be there for long,
    /// the one who orders the step stops it with <see cref="Cancel"/> when it is time.
    /// </summary>
    /// <param name="agent">The agent. It must not be carrying out a movement order.</param>
    /// <param name="target">Where to step, in the world coordinates.</param>
    /// <param name="face">The entity the agent keeps facing on the way, null if it turns to where it goes.</param>
    /// <returns>False if the step is not possible, the agent has not moved then.</returns>
    [PublicAPI]
    public bool Step(EntityUid agent, Vector2 target, EntityUid? face = null)
    {
        var graph = Transform(agent).ParentUid;

        if (!_gridQuery.HasComp(graph)
            || !_physicsQuery.TryComp(agent, out var body)
            || !_fixturesQuery.TryComp(agent, out var fixtures)
            || TryComp(agent, out RfSteererComponent? existing) && existing.Phase != RfMovePhase.None)
            return false;

        var from = Transform(agent).LocalPosition;
        var to = Vector2.Transform(target, _transform.GetInvWorldMatrix(graph));
        var margin = GetRadius(agent) + ShortcutClearance;

        GatherPathObstacles(agent, graph, new List<Vector2> { from, to }, margin, null, body, fixtures);
        _pathfinding.TryGetTagIndex(RfPathfindingSystem.ObstacleTag, out var obstacleTag);

        if (!IsStraight(graph, from, to, margin, obstacleTag, plain: false)
            || _pathfinding.GetPoly(new EntityCoordinates(graph, to)) is not { } poly
            || !IsFloor(poly.Data))
            return false;

        // A step is made in a fight, and it is a quick one.
        EnsureComp<RfSteererComponent>(agent).Sprint = true;

        var path = new List<PathNodeRef>
        {
            new(graph, default, 0, Box2.CenteredAround(from, new Vector2(0.5f, 0.5f))),
            new(graph, default, 0, Box2.CenteredAround(to, new Vector2(0.5f, 0.5f))),
        };

        if (!Follow(agent, new PathSearchResult(PathSearchStatus.Path, path, new()), new EntityCoordinates(graph, to), MinRange))
            return false;

        if (TryComp(agent, out RfSteeringComponent? steering))
            steering.FaceEntity = face;

        return true;
    }

    /// <summary>
    /// Whether the agent is making a <see cref="Step"/> now.
    /// </summary>
    [PublicAPI]
    public bool IsStepping(EntityUid agent)
        => TryComp(agent, out RfSteeringComponent? steering)
           && steering.Status == RfSteeringStatus.Moving
           && TryComp(agent, out RfSteererComponent? steerer)
           && steerer.Phase == RfMovePhase.None;
}
