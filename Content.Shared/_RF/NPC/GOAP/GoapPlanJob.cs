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

#if TOOLS
        public required GoapStateDebugDump StateBefore;
        public required GoapPreconditionDebugDump[] Preconditions;
        public required float H;
#endif
    }

    private readonly record struct ConditionCacheEntry(GoapState State, bool Result, GoapDebugDump? Dump);

    private sealed class MinHeap<T>
    {
        private readonly List<Entry> _data = new(128);

        private readonly record struct Entry(T Item, float Priority);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear() => _data.Clear();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Enqueue(T item, float priority)
        {
            _data.Add(new(item, priority));
            SiftUp(_data.Count - 1);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryDequeue(out T item)
        {
            if (_data.Count == 0)
            {
                item = default!;
                return false;
            }

            var root = _data[0];
            item = root.Item;

            var last = _data[^1];
            _data.RemoveAt(_data.Count - 1);

            if (_data.Count == 0)
                return true;

            _data[0] = last;
            SiftDown(0);
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

    private readonly MinHeap<Node> _openSet = new();

    // Best-known node for a state, bucketed by hash.
    private readonly Dictionary<int, List<Node>> _bestNodesByHash = new();

    // Closed states, bucketed by hash.
    private readonly Dictionary<int, List<GoapState>> _closedByHash = new();

    // Condition cache, bucketed by state hash + condition.
    private readonly Dictionary<(int StateHash, GoapCondition Condition), List<ConditionCacheEntry>> _conditionCache = new();

    protected override async Task<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)> Process()
    {
        _openSet.Clear();
        _bestNodesByHash.Clear();
        _closedByHash.Clear();
        _conditionCache.Clear();

#if TOOLS
        var debug = new GoapPlanDebugInfo
        {
            StartState = startState.GetStateDump(),
            GoalState = goalState.GetStateDump(),
            Nodes = new(),
            Actions = new(),
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

        var startClone = startState.ShallowClone();
        var startHeuristic = Heuristic(startClone, goalState);

        var startNode = new Node
        {
            State = startClone,
            Parent = null,
            TaskFromParent = null,
            TaskIdFromParent = null,
            G = 0f,
            F = startHeuristic,
#if TOOLS
            StateBefore = startState.GetStateDump(),
            Preconditions = Array.Empty<GoapPreconditionDebugDump>(),
            H = startHeuristic,
#endif
        };

        _openSet.Enqueue(startNode, startNode.F);
        SetBestNode(startNode);

        while (_openSet.TryDequeue(out var current))
        {
            // Check for timeout
            await SuspendIfOutOfTime();

            // Lazy deletion: skip nodes that are no longer the best known path to this state.
            if (!TryGetBestNode(current.State.CachedHash, current.State, out var bestForState)
                || !ReferenceEquals(bestForState, current))
                continue;

            if (IsClosed(current.State.CachedHash, current.State))
                continue;

            if (IsGoalSatisfied(current.State, goalState))
            {
#if TOOLS
                debug.Success = true;
                debug.TotalCost = current.G;
                var (plan, nodes, actions) = BuildPlan(current);
                debug.Actions = actions ?? new();
                debug.ElapsedTime = StopWatch.Elapsed;
                debug.Nodes.AddRange(nodes ?? new());

                // At this time, we only support breakpoints for nodes included in the plan,
                // since conditions are checked multiple times during planning and behavior may be unpredictable.
                foreach (var node in nodes ?? new())
                {
                    for (var i = 0; i < node.Preconditions.Length; i++)
                    {
                        goap.RaisePreconditionBreakpoint(target, node.NodeId, i, node.Preconditions[i].Result);
                    }
                }

                return (plan, debug);
#else
                return (BuildPlan(current).Plan, null);
#endif
            }

            MarkClosed(current.State.CachedHash, current.State);

            // Expand node by applying all executable tasks
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
#if TOOLS
                var stateBefore = current.State.GetStateDump();
                var preconditions = new List<GoapPreconditionDebugDump>(task.Preconditions.Count);
#endif

                var preconditionsMet = true;

                foreach (var cond in task.Preconditions)
                {
                    if (!TryGetCachedCondition(current.State.CachedHash, current.State, cond, out var cached))
                    {
                        cached = (goap.CheckCondition(target, current.State, cond, out var condDump), condDump);
                        SetCachedCondition(current.State.CachedHash, current.State, cond, cached);
#if TOOLS
                        debug.ConditionsChecked++;
#endif
                    }

                    if (!cached.Result)
                    {
                        preconditionsMet = false;
#if RELEASE
                        break;
#endif
                    }

#if TOOLS
                    preconditions.Add(new(cached.Dump ?? new(null, current.State.GetStateDump()), cached.Result));
#endif
                }

                if (!preconditionsMet)
                {
#if TOOLS
                    debug.Nodes.Add(new(
                        NodeId: i,
                        Compound: task.Compound,
                        Preconditions: preconditions.ToArray(),
                        StateBefore: stateBefore,
                        StateAfter: null,
                        TaskCost: current.G,
                        Heuristic: 0,
                        AddedToOpenList: false,
                        PreconditionsMet: false,
                        InPlan: false,
                        SkipReason: "Preconditions not met"));
#endif
                    continue;
                }

                // Apply effects to get the new state
                var newState = ApplyEffects(current.State, task.Effects);

#if TOOLS
                debug.NodesExpanded++;
                debug.EffectsApplied += task.Effects.Count;
#endif

                // Compute cost to reach new state
                var taskCost = TaskCost(target, current.State, task, goap);
                var newG = current.G + taskCost;

                // Skip if we already found a cheaper path to this state
                if (TryGetBestNode(newState.CachedHash, newState, out var existingNode) && existingNode.G <= newG)
                {
#if TOOLS
                    debug.Nodes.Add(new(
                        NodeId: i,
                        Compound: task.Compound,
                        Preconditions: preconditions.ToArray(),
                        StateBefore: stateBefore,
                        StateAfter: newState.GetStateDump(),
                        TaskCost: taskCost,
                        Heuristic: Heuristic(newState, goalState),
                        AddedToOpenList: false,
                        PreconditionsMet: true,
                        InPlan: false,
                        SkipReason: $"Cheaper path already exists (existing G={existingNode.G}, new G={newG})"));
                    debug.SkippedExpensiveNodes++;
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
#if TOOLS
                    StateBefore = stateBefore,
                    Preconditions = preconditions.ToArray(),
                    H = h,
#endif
                };

                _openSet.Enqueue(newNode, newNode.F);
                SetBestNode(newNode);

#if TOOLS
                debug.Nodes.Add(new(
                    NodeId: i,
                    Compound: task.Compound,
                    Preconditions: preconditions.ToArray(),
                    StateBefore: stateBefore,
                    StateAfter: newState.GetStateDump(),
                    TaskCost: taskCost,
                    Heuristic: h,
                    AddedToOpenList: true,
                    PreconditionsMet: true,
                    InPlan: false,
                    SkipReason: null));
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

        foreach (var action in task.Actions)
        {
            cost += goap.ActionCost(target, state, action);
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
    /// Currently, returns the number of unsatisfied goal conditions.
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
    private static (GoapPlan Plan, List<GoapNodeDebugEntry>? Nodes, List<GoapActionDebugInfo>? Debug) BuildPlan(Node goalNode)
    {
        // Collect tasks in reverse order
        var tasks = new List<(ExecutableGoapTask Task, int Id)>();
#if TOOLS
        var nodes = new List<GoapNodeDebugEntry>();
#endif
        var current = goalNode;

        while (current.Parent != null)
        {
            DebugTools.Assert(current.TaskFromParent != null, "Non-root node must have a task.");
            tasks.Add((current.TaskFromParent!.Value, current.TaskIdFromParent!.Value));

#if TOOLS
            nodes.Add(new GoapNodeDebugEntry(
                NodeId: current.TaskIdFromParent!.Value,
                Compound: current.TaskFromParent!.Value.Compound,
                Preconditions: current.Preconditions,
                StateBefore: current.StateBefore,
                StateAfter: current.State.GetStateDump(),
                TaskCost: current.G,
                Heuristic: current.H,
                AddedToOpenList: true,
                PreconditionsMet: true,
                InPlan: true,
                SkipReason: null));
#endif

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
        return (new GoapPlan(actions, 0), nodes, debug);
#else
        return new (GoapPlan(actions, 0), null, null);
#endif
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetBestNode(
        Dictionary<int, List<Node>> buckets,
        int hash,
        GoapState state,
        out Node node)
    {
        if (buckets.TryGetValue(hash, out var bucket))
        {
            foreach (var candidate in bucket)
            {
                if (!candidate.State.Equals(state))
                    continue;
                node = candidate;
                return true;
            }
        }

        node = default!;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetBestNode(int hash, GoapState state, out Node node)
        => TryGetBestNode(_bestNodesByHash, hash, state, out node);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetBestNode(Node node)
    {
        if (!_bestNodesByHash.TryGetValue(node.State.CachedHash, out var bucket))
        {
            bucket = new List<Node>(1);
            bucket.Add(node);
            _bestNodesByHash.Add(node.State.CachedHash, bucket);
            return;
        }

        for (var i = 0; i < bucket.Count; i++)
        {
            if (!bucket[i].State.Equals(node.State))
                continue;

            bucket[i] = node;
            return;
        }

        bucket.Add(node);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsClosed(int hash, GoapState state)
    {
        if (!_closedByHash.TryGetValue(hash, out var bucket))
            return false;

        foreach (var item in bucket)
        {
            if (item.Equals(state))
                return true;
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MarkClosed(int hash, GoapState state)
    {
        if (!_closedByHash.TryGetValue(hash, out var bucket))
        {
            bucket = new List<GoapState>(1);
            bucket.Add(state);
            _closedByHash.Add(hash, bucket);
            return;
        }

        foreach (var item in bucket)
        {
            if (item.Equals(state))
                return;
        }

        bucket.Add(state);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetCachedCondition(
        int stateHash,
        GoapState state,
        GoapCondition condition,
        out (bool Result, GoapDebugDump? Dump) result)
    {
        var key = (stateHash, condition);
        if (_conditionCache.TryGetValue(key, out var bucket))
        {
            foreach (var entry in bucket)
            {
                if (!entry.State.Equals(state))
                    continue;

                result = (entry.Result, entry.Dump);
                return true;
            }
        }

        result = default;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetCachedCondition(
        int stateHash,
        GoapState state,
        GoapCondition condition,
        (bool Result, GoapDebugDump? Dump) result)
    {
        var key = (stateHash, condition);
        if (!_conditionCache.TryGetValue(key, out var bucket))
        {
            bucket = new List<ConditionCacheEntry>(1);
            bucket.Add(new ConditionCacheEntry(state, result.Result, result.Dump));
            _conditionCache.Add(key, bucket);
            return;
        }

        for (var i = 0; i < bucket.Count; i++)
        {
            if (!bucket[i].State.Equals(state))
                continue;

            bucket[i] = new ConditionCacheEntry(state, result.Result, result.Dump);
            return;
        }

        bucket.Add(new ConditionCacheEntry(state, result.Result, result.Dump));
    }
}
