using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.CPUJob.JobQueues;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// A job that performs A* planning to find a sequence of GOAP actions
/// that transforms the agent's current state into the desired goal state.
/// </summary>
public sealed class GoapPlanJob(
    double maxTime,
    SharedGoapSystem goap,
    EntityUid target,
    GoapState startState,
    GoapState goalState,
    IReadOnlyList<ExecutableGoapTask> tasks,
    CancellationToken cancellation = default)
    : Job<GoapPlan?>(maxTime, cancellation)
{
    /// <summary>
    /// Represents a node in the A* search tree.
    /// </summary>
    private sealed class Node
    {
        /// <summary>The world state at this node.</summary>
        public required GoapState State;
        /// <summary>The cost from the start node to this node (g).</summary>
        public required float G;
        /// <summary>The estimated total cost from start to goal through this node (f = g + h).</summary>
        public required float F;
        /// <summary>The parent node in the search tree.</summary>
        public required Node? Parent;
        /// <summary>The task that was applied to reach this node from its parent.</summary>
        public required ExecutableGoapTask? TaskFromParent;
    }

    /// <summary>
    /// Equality comparer for <see cref="GoapState"/> that compares the contents
    /// of the state dictionaries.
    /// </summary>
    private sealed class GoapStateComparer : IEqualityComparer<GoapState>
    {
        public static readonly GoapStateComparer Instance = new();

        public bool Equals(GoapState? x, GoapState? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            if (x.Count != y.Count)
                return false;

            foreach (var (key, value) in x)
            {
                if (!y.TryGetValue<object>(key, out var other))
                    return false;

                if (!Equals(value, other))
                    return false;
            }

            return true;
        }

        public int GetHashCode(GoapState obj)
        {
            var hash = obj.Count;

            foreach (var (key, value) in obj)
            {
                var pairHash = HashCode.Combine(
                    key.GetHashCode(StringComparison.Ordinal),
                    value?.GetHashCode() ?? 0);

                hash ^= pairHash;
            }

            return hash;
        }
    }

    /// <inheritdoc/>
    protected override async Task<GoapPlan?> Process()
    {
        var openSet = new PriorityQueue<Node, float>();
        var closedSet = new HashSet<GoapState>(GoapStateComparer.Instance);
        var nodeCache = new Dictionary<GoapState, Node>(GoapStateComparer.Instance);

        var startNode = new Node
        {
            State = startState.ShallowClone(),
            Parent = null,
            TaskFromParent = null,
            G = 0f,
            F = Heuristic(startState, goalState)
        };

        openSet.Enqueue(startNode, startNode.F);
        nodeCache[startState] = startNode;

        while (openSet.TryDequeue(out var current, out _))
        {
            // Check for timeout
            await SuspendIfOutOfTime();

            // Goal reached?
            if (IsGoalSatisfied(current.State, goalState))
                return BuildPlan(current);

            if (!closedSet.Add(current.State))
                continue;

            // Expand node by applying all executable tasks
            foreach (var task in tasks)
            {
                // Check preconditions
                if (!goap.CheckCondition(target, current.State, task.Preconditions))
                    continue;

                // Apply effects to get the new state
                var newState = ApplyEffects(current.State, task.Effects);

                // Compute cost to reach new state
                var taskCost = TaskCost(target, current.State, task, goap);
                var newG = current.G + taskCost;

                // Skip if we already found a cheaper path to this state
                if (nodeCache.TryGetValue(newState, out var existingNode) && existingNode.G <= newG)
                    continue;

                var h = Heuristic(newState, goalState);
                var newNode = new Node
                {
                    State = newState,
                    Parent = current,
                    TaskFromParent = task,
                    G = newG,
                    F = newG + h
                };

                openSet.Enqueue(newNode, newNode.F);
                nodeCache[newState] = newNode;
            }
        }

        // No plan found
        return null;
    }

    /// <summary>
    /// Calculates the total cost of executing a task by summing the costs
    /// of all its constituent actions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float TaskCost(
        EntityUid target,
        GoapState state,
        ExecutableGoapTask task,
        SharedGoapSystem goap)
    {
        var cost = 0f;

        for (var i = 0; i < task.Actions.Count; i++)
        {
            cost += goap.ActionCost(target, state, task.Actions[i]);
        }

        return cost;
    }

    /// <summary>
    /// Checks whether the current state satisfies all conditions of the goal state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsGoalSatisfied(GoapState current, GoapState goal)
    {
        foreach (var kv in goal)
        {
            if (!current.TryGetValue<object>(kv.Key, out var curValue) || !Equals(curValue, kv.Value))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Creates a new state by applying the effects of a task to the given state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GoapState ApplyEffects(GoapState state, GoapEffectsList effects)
    {
        var newState = state.ShallowClone();

        foreach (var effect in effects.Effects)
        {
            newState.SetValue(effect.Key, effect.Value);
        }

        return newState;
    }

    /// <summary>
    /// Heuristic function that estimates the cost from the current state to the goal.
    /// Currently returns the number of unsatisfied goal conditions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Heuristic(GoapState current, GoapState goal)
    {
        var unsatisfied = 0;
        foreach (var kv in goal)
        {
            if (!current.TryGetValue<object>(kv.Key, out var curValue) || !Equals(curValue, kv.Value))
                unsatisfied++;
        }
        return unsatisfied;
    }

    /// <summary>
    /// Reconstructs the plan by walking back from the goal node to the start.
    /// Returns a flat list of actions to execute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GoapPlan BuildPlan(Node goalNode)
    {
        // Collect tasks in reverse order
        var tasks = new List<ExecutableGoapTask>();
        var current = goalNode;

        while (current.Parent != null)
        {
            DebugTools.Assert(current.TaskFromParent != null, "Non-root node must have a task.");
            tasks.Add(current.TaskFromParent!.Value);
            current = current.Parent;
        }

        tasks.Reverse();

        // Flatten tasks into a sequence of actions
        var actions = new List<GoapAction>();

        foreach (var task in tasks)
        {
            actions.AddRange(task.Actions);
        }

        return new GoapPlan(actions, 0);
    }
}
