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
    : Job<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)>(maxTime, cancellation)
{
    /// <summary>
    /// Represents a node in the A* search tree.
    /// </summary>
    private sealed class Node
    {
        /// <summary>
        /// The world state at this node.
        /// </summary>
        public required GoapState State;

        /// <summary>
        /// The cost from the start node to this node (g).
        /// </summary>
        public required float G;

        /// <summary>
        /// The estimated total cost from start to goal through this node (f = g + h).
        /// </summary>
        public required float F;

        /// <summary>
        /// The parent node in the search tree.
        /// </summary>
        public required Node? Parent;

        /// <summary>
        /// The task that was applied to reach this node from its parent.
        /// </summary>
        public required ExecutableGoapTask? TaskFromParent;

        public required int? TaskIdFromParent;
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
                    value.GetHashCode());

                hash ^= pairHash;
            }

            return hash;
        }
    }

    internal sealed class MinHeap<T>
    {
        private readonly List<Entry> _data = new(128);

        public int Count => _data.Count;

        private readonly record struct Entry(T Item, float Priority);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item, float priority)
        {
            _data.Add(new(item, priority));
            SiftUp(_data.Count - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item, out float priority)
        {
            if (_data.Count == 0)
            {
                item = default!;
                priority = default;
                return false;
            }

            var root = _data[0];
            item = root.Item;
            priority = root.Priority;

            var last = _data[^1];
            _data.RemoveAt(_data.Count - 1);

            if (_data.Count > 0)
            {
                _data[0] = last;
                SiftDown(0);
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftUp(int index)
        {
            var data = _data;

            while (index > 0)
            {
                var parent = (index - 1) >> 1;

                if (data[index].Priority >= data[parent].Priority)
                    break;

                (data[index], data[parent]) = (data[parent], data[index]);
                index = parent;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SiftDown(int index)
        {
            var data = _data;
            var count = data.Count;

            while (true)
            {
                var left = (index << 1) + 1;
                if (left >= count)
                    break;

                var right = left + 1;
                var smallest = left;

                if (right < count && data[right].Priority < data[left].Priority)
                    smallest = right;

                if (data[index].Priority <= data[smallest].Priority)
                    break;

                (data[index], data[smallest]) = (data[smallest], data[index]);
                index = smallest;
            }
        }
    }

    /// <inheritdoc/>
    protected override async Task<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)> Process()
    {
        var openSet = new MinHeap<Node>();
        var closedSet = new HashSet<GoapState>(GoapStateComparer.Instance);
        var nodeCache = new Dictionary<GoapState, Node>(GoapStateComparer.Instance);

        var startNode = new Node
        {
            State = startState.ShallowClone(),
            Parent = null,
            TaskFromParent = null,
            TaskIdFromParent = null,
            G = 0f,
            F = Heuristic(startState, goalState),
        };

#if TOOLS
        var debug = new GoapPlanDebugInfo
        {
            StartState = startState.GetStateDump(),
            GoalState = goalState.GetStateDump(),
            Nodes = new(),
        };
#endif

        if (goalState.Count == 0)
        {
#if TOOLS
            debug.Success = false;
            debug.ElapsedTime = StopWatch.Elapsed;
            return (null, debug);
#else
            return (null, null);
#endif
        }

        openSet.Enqueue(startNode, startNode.F);
        nodeCache[startState] = startNode;

        while (openSet.TryDequeue(out var current, out _))
        {
            // Check for timeout
            await SuspendIfOutOfTime();

            // Goal reached?
            if (IsGoalSatisfied(current.State, goalState))
            {
#if TOOLS
                debug.Success = true;
                debug.TotalCost = current.G;
                var (plan, actions) = BuildPlan(current);
                debug.Actions = actions ?? new();
                debug.ElapsedTime = StopWatch.Elapsed;
                return (plan, debug);
#else
                return (BuildPlan(current).Plan, null);
#endif
            }

            if (!closedSet.Add(current.State))
                continue;

            // Expand node by applying all executable tasks
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
#if TOOLS
                var stateBefore = current.State.GetStateDump();
                var preconditions = new List<GoapPreconditionDebugDump>();
                debug.NodesExpanded++;
#endif

                // Check preconditions
#if TOOLS
                // In TOOLS dump all conditions
                var preconditionsMet = true;
                foreach (var cond in task.Preconditions)
                {
                    var result = goap.CheckCondition(target, current.State, cond, out var condDump);

                    if (!result)
                        preconditionsMet = false;

                    preconditions.Add(new(condDump ?? new(null, current.State.GetStateDump()), result));
                }
#else
                var preconditionsMet = goap.CheckCondition(target, current.State, task.Preconditions);
#endif

                if (!preconditionsMet)
                {
#if TOOLS
                    debug.Nodes.Add(new(
                        i,
                        task.Compound,
                        preconditions.ToArray(),
                        stateBefore,
                        null,
                        current.G,
                        0,
                        false,
                        false,
                        "Preconditions not met"));
#endif
                    continue;
                }

                // Apply effects to get the new state
                var newState = ApplyEffects(current.State, task.Effects);

                // Compute cost to reach new state
                var taskCost = TaskCost(target, current.State, task, goap);
                var newG = current.G + taskCost;

                // Skip if we already found a cheaper path to this state
                if (nodeCache.TryGetValue(newState, out var existingNode) && existingNode.G <= newG)
                {
#if TOOLS
                    debug.Nodes.Add(new(
                        i,
                        task.Compound,
                        preconditions.ToArray(),
                        stateBefore,
                        newState.GetStateDump(),
                        taskCost,
                        0,
                        false,
                        true,
                        $"Cheaper path already exists (existing G={existingNode.G}, new G={newG})"));
#endif
                    continue;
                }

                var h = Heuristic(newState, goalState);
                var newNode = new Node
                {
                    State = newState,
                    Parent = current,
                    TaskFromParent = task,
                    TaskIdFromParent = i,
                    G = newG,
                    F = newG + h,
                };

                openSet.Enqueue(newNode, newNode.F);
                nodeCache[newState] = newNode;

#if TOOLS
                debug.Nodes.Add(new(
                    i,
                    task.Compound,
                    preconditions.ToArray(),
                    stateBefore,
                    newState.GetStateDump(),
                    taskCost,
                    h,
                    true,
                    true,
                    null));
#endif
            }
        }

        // No plan found
#if TOOLS
        debug.Success = false;
        debug.ElapsedTime = StopWatch.Elapsed;
        return (null, debug);
#else
        return (null, null);
#endif
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
    private static GoapState ApplyEffects(GoapState state, GoapState effects)
    {
        var newState = state.ShallowClone();
        newState.OverwriteFrom(effects);
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
    private static (GoapPlan Plan, List<GoapActionDebugInfo>? Debug) BuildPlan(Node goalNode)
    {
        // Collect tasks in reverse order
        var tasks = new List<(ExecutableGoapTask Task, int Id)>();
        var current = goalNode;

        while (current.Parent != null)
        {
            DebugTools.Assert(current.TaskFromParent != null, "Non-root node must have a task.");
            tasks.Add((current.TaskFromParent!.Value, current.TaskIdFromParent!.Value));
            current = current.Parent;
        }

        tasks.Reverse();

        // Flatten tasks into a sequence of actions
        var actions = new List<GoapAction>();

        foreach (var (task, _) in tasks)
        {
            actions.AddRange(task.Actions);
        }

#if TOOLS
        var debug = new List<GoapActionDebugInfo>();

        foreach (var (task, id) in tasks)
        {
            for (var j = 0; j < task.Actions.Count; j++)
            {
                debug.Add(new GoapActionDebugInfo(
                    id,
                    j,
                    null,
                    null,
                    null,
                    new()));
            }
        }
        return (new GoapPlan(actions, 0), debug);
#else
        return new (GoapPlan(actions, 0), null);
#endif
    }
}
