using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.Server._RF.NPC.Steering.Systems;

// Movement orders: finding the path and following it, searching for it again when it stops being right.
public sealed partial class RfSteeringSystem
{
    [Dependency] private RfPathfindingSystem _pathfinding = null!;
    [Dependency] private SharedUtilityAiSystem _utility = null!;
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private EntityQuery<PathProfilesComponent> _profilesQuery;

    // The agents that have got to a node with a goal. The goal is started on the next update and not in the middle
    // of the update of the action, because it replaces the plan that the action is a part of.
    private readonly List<EntityUid> _goalStarts = new();

    // For how long after an agent has arrived the restart margin is in effect.
    private static readonly TimeSpan RestartWindow = TimeSpan.FromSeconds(2);

    // How close to the node of a goal that is performed after it the agent has to come to be counted as having gone through
    // it, how far past it the goal is performed (the agent is still within reach of the node then, it brakes and the goal
    // has to be able to go back), and how far from the place an entity of the node is looked for.
    private const float NodeReached = 0.8f;
    private const float AfterRange = 1f;
    private const float EntityRange = 0.5f;

    // How many places where the agent got stuck are remembered.
    private const int MaxAvoid = 6;

    private void UpdateOrders()
    {
        if (_goalStarts.Count == 0)
            return;

        foreach (var uid in _goalStarts)
        {
            if (TryComp(uid, out RfSteererComponent? steerer) && TryComp(uid, out GoapComponent? goap))
                StartGoal((uid, steerer, goap));
        }

        _goalStarts.Clear();
    }

    private void StartGoal(Entity<RfSteererComponent, GoapComponent> agent)
    {
        var steerer = agent.Comp1;

        if (steerer.Phase != RfMovePhase.StartingGoal || steerer.GoalToStart is not { } node)
            return;

        steerer.Phase = RfMovePhase.None;
        steerer.PendingGoal = null;
        steerer.GoalToStart = null;
        steerer.Afters.Clear();

        var goal = node.Info;
        var state = agent.Comp2.State;
        var place = new EntityCoordinates(node.Node.Graph, node.Node.Box.Center);
        state.SetValue(goal.TargetKey, place);

        // The goals that are for an entity, such as a door, need it and not the place.
        if (goal.EntityTag is { } tag && goal.EntityKey is { } key)
        {
            var placeWorld = _transform.ToMapCoordinates(place).Position;
            var nearest = float.MaxValue;

            foreach (var found in _lookup.GetEntitiesInRange<PathTagsComponent>(place, EntityRange))
            {
                if (!_pathfinding.HasTag(found.Owner, tag))
                    continue;

                var distance = (_transform.GetWorldPosition(found) - placeWorld).LengthSquared();

                if (distance >= nearest)
                    continue;

                nearest = distance;
                state.SetValue(key, found.Owner);
            }
        }

        // What the agent was doing is put aside and goes on when the goal of the node is done.
        _utility.SuspendGoal(agent.Owner, goal.Goal);
    }

    /// <summary>
    /// Starts the goal that is performed after its node, if the agent has gone through the node and is far enough from it.
    /// </summary>
    /// <returns>True, if a goal has been started.</returns>
    private bool CheckAfters(Entity<GoapComponent> agent, RfSteererComponent steerer, RfSteeringComponent steering)
    {
        if (steerer.Afters.Count == 0 || Transform(agent).ParentUid != steering.Graph)
            return false;

        var position = Transform(agent).LocalPosition;

        foreach (var after in steerer.Afters)
        {
            var distance = Vector2.Distance(position, after.Goal.Node.Box.Center);

            if (distance < NodeReached)
            {
                after.Reached = true;
                continue;
            }

            if (!after.Reached || distance < after.Range)
                continue;

            steerer.Afters.Remove(after);
            steerer.GoalToStart = after.Goal;
            steerer.Phase = RfMovePhase.StartingGoal;
            _goalStarts.Add(agent);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Carries out the order to get to the target. Call it every update of the action until it is over.
    /// The path is searched for by the profiles of the agent, followed, and searched for again if the agent
    /// gets stuck, leaves it or the target moves away.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="target">Where to go.</param>
    /// <param name="range">How close to the target the agent needs to get.</param>
    /// <param name="stopOnSight">Is it enough for the agent to see the target for a while to have got there.</param>
    /// <param name="query">What the path is searched for with, null for the default of the agent.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Continuing"/> while the agent is on the way,
    /// <see cref="GoapActionResult.Finished"/> when it has got there and stopped,
    /// <see cref="GoapActionResult.Failed"/> when it cannot.
    /// </returns>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> agent,
        EntityCoordinates target,
        float range,
        bool stopOnSight = false,
        ProtoId<PathQueryPrototype>? query = null)
        => Move(agent, target, null, range, stopOnSight, query);

    /// <summary>
    /// Carries out the order to get to the entity. The range is counted from the region the entity takes up and not
    /// from its center, so an entity that is big and solid, a wall or a machine, can be reached: the agent comes up
    /// to it and does not try to get into its center. The target is followed if it moves.
    /// </summary>
    /// <inheritdoc cref="Move(Entity{GoapComponent}, EntityCoordinates, float, bool, ProtoId{PathQueryPrototype}?)"/>
    [PublicAPI]
    public GoapActionResult Move(
        Entity<GoapComponent> agent,
        EntityUid target,
        float range,
        bool stopOnSight = false,
        ProtoId<PathQueryPrototype>? query = null)
        => TerminatingOrDeleted(target)
            ? GoapActionResult.Failed
            : Move(agent, Transform(target).Coordinates, target, range, stopOnSight, query);

    private GoapActionResult Move(
        Entity<GoapComponent> agent,
        EntityCoordinates target,
        EntityUid? entity,
        float range,
        bool stopOnSight,
        ProtoId<PathQueryPrototype>? query)
    {
        if (!_profilesQuery.TryComp(agent, out var profiles))
            return GoapActionResult.Failed;

        var steerer = EnsureComp<RfSteererComponent>(agent);

        // The goal of the node is about to be started. The plan of this order is replaced when the new one is ready, until
        // then the agent stays where it is. The plan of the goal itself can move, such as to pick up a tool.
        if (steerer.Phase == RfMovePhase.StartingGoal)
            return GoapActionResult.Continuing;

        var search = new Entity<PathProfilesComponent, GoapComponent>(agent, profiles, agent.Comp);

        if (steerer.Phase == RfMovePhase.None)
        {
            // Nothing to do if the agent is there already, a steering that is braking goes on by itself. An agent that
            // has just got there is not sent back for being a little farther than the range, it would twitch.
            var recent = _timing.CurTime - steerer.FinishedAt < RestartWindow;

            if (IsInRange(agent, target, entity, recent ? range + steerer.RestartMargin : range))
                return GoapActionResult.Finished;

            steerer.Target = target;
            steerer.TargetEntity = entity;
            steerer.Range = range;
            steerer.ActiveQuery = query ?? steerer.Query;
            steerer.Repaths = 0;
            steerer.Avoid.Clear();
            steerer.NoSmoothing = false;

            if (!Search(search, steerer))
                return GoapActionResult.Failed;

            steerer.Phase = RfMovePhase.Searching;
        }
        else if (profiles.RequestStatus == PathRequestStatus.None && Moved(steerer, target))
        {
            // The target has moved, the agent goes on along the old path while the new one is searched for.
            steerer.Target = target;

            if (!Search(search, steerer))
                return Fail(agent, steerer);
        }

        if (profiles.RequestStatus != PathRequestStatus.None)
        {
            if (!_pathfinding.TryGetResult(search, out var result, out _))
                return GoapActionResult.Continuing;

            _pathfinding.CancelPath(new Entity<PathProfilesComponent?>(agent.Owner, profiles));

            if (result.Status == PathSearchStatus.NoPath)
                return Fail(agent, steerer);

            // The agent goes only to the first node that has a goal of the kind that is performed before the node, the
            // rest of the path is found again after it.
            // A goal at the node the agent is standing in is not performed, it is already there: in the doorway of a door that
            // is being opened or closed, for example, and it would not get out of it.
            var first = result.Path.Count > 0 ? result.Path[0] : default;
            var beforeIndex = result.Goals.FindIndex(g => g.Phase == PathGoalPhase.Before && g.Node != first);
            steerer.PendingGoal = beforeIndex >= 0 ? result.Goals[beforeIndex] : null;
            var stop = steerer.Range;
            var bounds = steerer.SearchedBounds;
            var ignore = steerer.TargetEntity;

            if (steerer.PendingGoal is { } goal)
            {
                var index = result.Path.FindIndex(node => node == goal.Node);

                if (index < 0)
                    return Fail(agent, steerer);

                result = new PathSearchResult(
                    PathSearchStatus.PartialPath,
                    result.Path.GetRange(0, index + 1),
                    result.Goals);

                // The distance to stop at is in the state of the agent, the way the movement ranges are.
                if (_goap.TryGetValue(agent.Comp.State, goal.Info.RangeKey, out var key))
                    stop = key;

                bounds = null;
                ignore = null;
            }

            // The goals of the kind that is performed after the node, on the part of the path that is followed.
            steerer.Afters.Clear();

            foreach (var after in result.Goals)
            {
                if (after.Phase != PathGoalPhase.After || !result.Path.Contains(after.Node))
                    continue;

                steerer.Afters.Add(new AfterGoal { Goal = after, Range = AfterRange });
            }

            if (!Follow(agent, result, steerer.SearchedTarget, stop, bounds, ignore))
                return Fail(agent, steerer);

            steerer.Partial = steerer.PendingGoal == null && result.Status == PathSearchStatus.PartialPath;
            steerer.Phase = RfMovePhase.Following;
        }

        if (!TryComp(agent, out RfSteeringComponent? steering))
            return Fail(agent, steerer);

        steering.Sight = stopOnSight && steerer.PendingGoal == null ? target : null;

        switch (steering.Status)
        {
            case RfSteeringStatus.Moving:
                CheckAfters(agent, steerer, steering);
                return GoapActionResult.Continuing;
            case RfSteeringStatus.InRange when steerer.PendingGoal != null:
                // The agent is at the node whose goal it has to perform.
                steerer.GoalToStart = steerer.PendingGoal;
                steerer.Phase = RfMovePhase.StartingGoal;
                _goalStarts.Add(agent);
                return GoapActionResult.Continuing;
            case RfSteeringStatus.InRange:
                // A partial path ends at the closest point, which is not where the agent was asked to get.
                return steerer.Partial && !stopOnSight && !IsInRange(agent, target, entity, range)
                    ? Fail(agent, steerer)
                    : Done(agent, steerer);
            default:
                // Stuck or off the path, the path is searched for again, and not by the place where it did not work.
                if (steering.Status == RfSteeringStatus.Stuck)
                {
                    steerer.NoSmoothing = true;

                    if (steerer.Avoid.Count >= MaxAvoid)
                        steerer.Avoid.RemoveAt(0);

                    steerer.Avoid.Add(new PathAvoid(
                        steering.Graph,
                        Transform(agent).LocalPosition,
                        steerer.AvoidRadius,
                        steerer.AvoidCost));
                }

                Log.Debug($"{ToPrettyString(agent)} is {steering.Status} at {Transform(agent).LocalPosition}, searching again ({steerer.Repaths + 1})");
                steerer.Target = target;

                return ++steerer.Repaths > steerer.MaxRepaths || !Search(search, steerer)
                    ? Fail(agent, steerer)
                    : GoapActionResult.Continuing;
        }
    }

    /// <summary>
    /// Drops the order of the agent. It brakes and stops, and the path request is cancelled.
    /// </summary>
    [PublicAPI]
    public void Cancel(EntityUid agent)
    {
        if (TryComp(agent, out RfSteererComponent? steerer))
        {
            steerer.Phase = RfMovePhase.None;
            steerer.PendingGoal = null;
            steerer.GoalToStart = null;
            steerer.Afters.Clear();
        }

        if (_profilesQuery.TryComp(agent, out var profiles))
            _pathfinding.CancelPath((agent, profiles));

        Stop(agent);
    }

    private bool Search(Entity<PathProfilesComponent, GoapComponent> agent, RfSteererComponent steerer)
    {
        var target = steerer.Target;
        Box2? bounds = null;
        var range = steerer.Range;

        // The region of the target is taken in the coordinates of the grid of the agent, the path is on it. A target that is gone
        // (a leader that is followed) is a place where it was.
        if (steerer.TargetEntity is { } entity
            && !TerminatingOrDeleted(entity)
            && Transform(agent).GridUid is { } grid
            && Transform(entity).MapID == Transform(agent).MapID)
        {
            target = _transform.WithEntityId(target, grid);
            bounds = _transform.GetInvWorldMatrix(grid).TransformBox(_lookup.GetWorldAABB(entity));
            range = GetRange(agent, steerer.Range, true);
        }

        steerer.SearchedTarget = target;
        steerer.SearchedBounds = bounds;

        return _pathfinding.RequestPath(agent, steerer.ActiveQuery, target, range, targetBounds: bounds, avoid: steerer.Avoid);
    }

    /// <summary>
    /// The range the agent can get to a solid entity at: it cannot come closer than its own size.
    /// </summary>
    private float GetRange(EntityUid agent, float range, bool solid)
        => solid ? Math.Max(range, GetRadius(agent) + RangeMargin) : range;

    private bool Moved(RfSteererComponent steerer, EntityCoordinates target)
        => !steerer.SearchedTarget.TryDistance(EntityManager, _transform, target, out var distance)
           || distance > steerer.RetargetDistance;

    private bool IsInRange(EntityUid agent, EntityCoordinates target, EntityUid? entity, float range)
    {
        if (entity is not { } uid || Transform(uid).MapID != Transform(agent).MapID)
        {
            return Transform(agent).Coordinates.TryDistance(EntityManager, _transform, target, out var distance)
                   && distance <= range;
        }

        // The distance from the agent to the region of the entity.
        var box = _lookup.GetWorldAABB(uid);
        var position = _transform.GetWorldPosition(agent);
        var dx = Math.Max(Math.Max(box.Left - position.X, 0f), position.X - box.Right);
        var dy = Math.Max(Math.Max(box.Bottom - position.Y, 0f), position.Y - box.Top);

        return MathF.Sqrt(dx * dx + dy * dy) <= GetRange(agent, range, true);
    }

    private GoapActionResult Done(EntityUid agent, RfSteererComponent steerer)
    {
        steerer.FinishedAt = _timing.CurTime;
        Cancel(agent);
        return GoapActionResult.Finished;
    }

    private GoapActionResult Fail(EntityUid agent, RfSteererComponent steerer)
    {
        Cancel(agent);
        return GoapActionResult.Failed;
    }
}
