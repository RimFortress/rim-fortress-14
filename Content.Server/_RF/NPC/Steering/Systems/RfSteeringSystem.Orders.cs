using Content.Server._RF.NPC.Pathfinding.Systems;
using Content.Server._RF.NPC.Steering.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.UtilityAi;
using Content.Shared._RF.NPC.UtilityAi.Systems;
using JetBrains.Annotations;
using Robust.Shared.Map;

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

    // The goal of the node that has not been performed: it could not be planned, or it was replaced.
    [SubscribeLocalEvent]
    private void OnGoalEnded(Entity<RfSteererComponent> ent, ref UtilityAiGoalFinished args)
    {
        if (ent.Comp.NodeGoal != args.Goal)
            return;

        ent.Comp.NodeGoal = null;
        ent.Comp.ReturnGoal = null;
    }

    /// <summary>
    /// When the goal of a node is finished the agent returns to what it was doing, which brings it to the target
    /// along a new path: the obstacle is gone, so the path is different.
    /// </summary>
    [SubscribeLocalEvent]
    private void OnGoalFinished(Entity<RfSteererComponent> ent, ref BeforeUtilityAiGoalFinished args)
    {
        if (ent.Comp.NodeGoal != args.Goal)
            return;

        var back = ent.Comp.ReturnGoal;
        ent.Comp.NodeGoal = null;
        ent.Comp.ReturnGoal = null;

        // If the goal failed, the agent chooses by itself, going back would lead to the same node.
        if (args.Reason != UtilityAiGoalFinishReason.Finished || back == null)
            return;

        _utility.SetGoal(ent.Owner, back.Value);
        args.Handled = true;
    }

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

        if (steerer.Phase != RfMovePhase.StartingGoal || steerer.PendingGoal is not { } goal)
            return;

        steerer.Phase = RfMovePhase.None;
        steerer.PendingGoal = null;

        var state = agent.Comp2.State;
        state.SetValue(goal.TargetKey, new EntityCoordinates(goal.Node.Graph, goal.Node.Box.Center));

        _utility.TryGetCurrentGoal(agent.Owner, out var current);

        // A new goal is planned only when there is no plan, the plan of the order has to be given up.
        if (agent.Comp2.Plan != null)
            _goap.PlanShutdown((agent.Owner, agent.Comp2), GoapPlanFinishReason.Interrupted);

        steerer.NodeGoal = goal.Goal;
        steerer.ReturnGoal = current;
        _utility.SetGoal(agent.Owner, goal.Goal);
    }

    /// <summary>
    /// Carries out the order to get to the target. Call it every update of the action until it is over.
    /// The path is searched for by the profiles of the agent, followed, and searched for again if the agent
    /// gets stuck, leaves it or the target moves away.
    /// </summary>
    /// <param name="agent">The agent.</param>
    /// <param name="target">Where to go.</param>
    /// <param name="range">How close to the target the agent needs to get.</param>
    /// <returns>
    /// <see cref="GoapActionResult.Continuing"/> while the agent is on the way,
    /// <see cref="GoapActionResult.Finished"/> when it has got there and stopped,
    /// <see cref="GoapActionResult.Failed"/> when it cannot.
    /// </returns>
    [PublicAPI]
    public GoapActionResult Move(Entity<GoapComponent> agent, EntityCoordinates target, float range)
    {
        if (!_profilesQuery.TryComp(agent, out var profiles))
            return GoapActionResult.Failed;

        var steerer = EnsureComp<RfSteererComponent>(agent);

        // The goal of the node is about to be started, or is being performed. The plan of this order is replaced
        // when the new one is ready, until then the agent stays where it is.
        if (steerer.Phase == RfMovePhase.StartingGoal || steerer.NodeGoal != null)
            return GoapActionResult.Continuing;

        var search = new Entity<PathProfilesComponent, GoapComponent>(agent, profiles, agent.Comp);

        if (steerer.Phase == RfMovePhase.None)
        {
            steerer.Target = target;
            steerer.Range = range;
            steerer.Repaths = 0;

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

            // The agent goes only to the first node that has a goal, the rest of the path is found again after it.
            steerer.PendingGoal = result.Goals.Count > 0 ? result.Goals[0] : null;

            if (steerer.PendingGoal is { } goal)
            {
                var index = result.Path.FindIndex(node => node == goal.Node);

                if (index < 0)
                    return Fail(agent, steerer);

                result = new PathSearchResult(
                    PathSearchStatus.PartialPath,
                    result.Path.GetRange(0, index + 1),
                    result.Goals);
            }

            // The distance to stop at is in the state of the agent, the way the movement ranges are.
            var stop = steerer.PendingGoal is { } pending && _goap.TryGetValue(agent.Comp.State, pending.RangeKey, out var key)
                ? key
                : range;

            if (!Follow(agent, result, steerer.SearchedTarget, stop))
                return Fail(agent, steerer);

            steerer.Partial = steerer.PendingGoal == null && result.Status == PathSearchStatus.PartialPath;
            steerer.Phase = RfMovePhase.Following;
        }

        if (!TryComp(agent, out RfSteeringComponent? steering))
            return Fail(agent, steerer);

        switch (steering.Status)
        {
            case RfSteeringStatus.Moving:
                return GoapActionResult.Continuing;
            case RfSteeringStatus.InRange when steerer.PendingGoal != null:
                // The agent is at the node whose goal it has to perform.
                steerer.Phase = RfMovePhase.StartingGoal;
                _goalStarts.Add(agent);
                return GoapActionResult.Continuing;
            case RfSteeringStatus.InRange:
                // A partial path ends at the closest point, which is not where the agent was asked to get.
                return steerer.Partial && !IsInRange(agent, target, range)
                    ? Fail(agent, steerer)
                    : Done(agent, steerer);
            default:
                // Stuck or off the path, the path is searched for again.
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
        }

        if (_profilesQuery.TryComp(agent, out var profiles))
            _pathfinding.CancelPath((agent, profiles));

        Stop(agent);
    }

    private bool Search(Entity<PathProfilesComponent, GoapComponent> agent, RfSteererComponent steerer)
    {
        steerer.SearchedTarget = steerer.Target;
        return _pathfinding.RequestPath(agent, steerer.Target, steerer.Range);
    }

    private bool Moved(RfSteererComponent steerer, EntityCoordinates target)
        => !steerer.SearchedTarget.TryDistance(EntityManager, _transform, target, out var distance)
           || distance > steerer.RetargetDistance;

    private bool IsInRange(EntityUid agent, EntityCoordinates target, float range)
        => Transform(agent).Coordinates.TryDistance(EntityManager, _transform, target, out var distance)
           && distance <= range;

    private GoapActionResult Done(EntityUid agent, RfSteererComponent steerer)
    {
        Cancel(agent);
        return GoapActionResult.Finished;
    }

    private GoapActionResult Fail(EntityUid agent, RfSteererComponent steerer)
    {
        Cancel(agent);
        return GoapActionResult.Failed;
    }
}
