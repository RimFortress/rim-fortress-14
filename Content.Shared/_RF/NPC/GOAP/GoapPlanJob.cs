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
    GoapStaticGraph graph,
    CancellationToken cancellation = default,
    bool collectDebug = false)
    : Job<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)>(maxTime, cancellation)
{
    #region Structs

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

        public required GoapStateDebugDump StateBefore;
        public required GoapPreconditionDebugDump[] Preconditions;
        public required float H;
    }

    private readonly record struct ConditionCacheEntry(bool Result, GoapDebugDump? Dump);

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

    #endregion

    private readonly MinHeap<Node> _openSet = new();

    // Best-known node for a state, bucketed by hash.
    private readonly Dictionary<GoapState, Node> _bestNodes = new();

    // Closed states, bucketed by hash.
    private readonly HashSet<GoapState> _closed = new();

    // Condition cache, bucketed by state hash + condition.
    private readonly Dictionary<(GoapState, GoapCondition), ConditionCacheEntry> _conditionCache = new();

    protected override async Task<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)> Process()
    {
        _openSet.Clear();
        _bestNodes.Clear();
        _closed.Clear();
        _conditionCache.Clear();

        GoapPlanDebugInfo? debugInfo = null;

        // -- Debug
        if (collectDebug)
        {
            debugInfo = new GoapPlanDebugInfo
            {
                StartState = startState.GetStateDump(),
                GoalState = goalState.GetStateDump(),
                Nodes = new(),
                Actions = new(),
            };
        }
        // -- Debug

        if (goalState.Count == 0 || graph.Nodes.Count == 0)
        {
            // -- Debug
            if (debugInfo is { } debug)
            {
                debug.Success = false;
                debug.ElapsedTime = StopWatch.Elapsed;
            }
            // -- Debug
            return (null, debugInfo);
        }

        // Precompute the candidate task sets once per planning job.
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
            // -- Debug
            StateBefore = collectDebug ? startState.GetStateDump() : new(),
            Preconditions = Array.Empty<GoapPreconditionDebugDump>(),
            H = startHeuristic,
            // -- Debug
        };

        _openSet.Enqueue(startNode, startNode.F);
        _bestNodes[startClone] = startNode;

        while (_openSet.TryDequeue(out var current))
        {
            // Skip if we have already closed this state.
            if (_closed.Contains(current.State))
                continue;

            if (IsGoalSatisfied(current.State, goalState))
            {
                // -- Debug
                if (debugInfo is { } debug)
                {
                    debug.Success = true;
                    debug.TotalCost = current.G;
                    var (plan, nodes, actions) = BuildPlan(current, collectDebug);
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
                }
                // -- Debug
                return (BuildPlan(current, collectDebug).Plan, null);
            }

            _closed.Add(current.State);

            ExpandCandidates(
                current,
                current.TaskIdFromParent is { } fromId &&
                graph.CandidatesByNodeId.TryGetValue(fromId, out var cachedCandidates)
                    ? cachedCandidates
                    : graph.RootCandidates,
                debugInfo);

            // Check for timeout
            await SuspendIfOutOfTime();
        }

        // No plan found
        // -- Debug
        if (debugInfo != null)
        {
            var debug = debugInfo.Value;
            debug.Success = false;
            debug.ElapsedTime = StopWatch.Elapsed;
        }
        // -- Debug
        return (null, debugInfo);
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
    private static (GoapPlan Plan, List<GoapNodeDebugEntry>? Nodes, List<GoapActionDebugInfo>? Debug) BuildPlan(
        Node goalNode,
        bool collectDebug)
    {
        // Collect tasks in reverse order
        var tasks = new List<(ExecutableGoapTask Task, int Id)>();
        var current = goalNode;

        // -- Debug
        List<GoapNodeDebugEntry>? nodes = null;
        List<GoapActionDebugInfo>? debug = null;
        if (collectDebug)
        {
            nodes = new List<GoapNodeDebugEntry>();
            debug = new List<GoapActionDebugInfo>();
        }
        // -- Debug

        while (current.Parent != null)
        {
            DebugTools.Assert(current.TaskFromParent != null, "Non-root node must have a task.");
            tasks.Add((current.TaskFromParent!.Value, current.TaskIdFromParent!.Value));

            // -- Debug
            nodes?.Add(new GoapNodeDebugEntry(
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
            // -- Debug

            current = current.Parent;
        }

        tasks.Reverse();

        // Flatten tasks into a sequence of actions
        var actions = new List<GoapAction>();

        foreach (var (task, _) in tasks)
        {
            actions.AddRange(task.Actions);
        }

        // -- Debug
        if (collectDebug)
        {
            foreach (var (task, id) in tasks)
            {
                for (var j = 0; j < task.Actions.Count; j++)
                {
                    debug?.Add(new GoapActionDebugInfo(
                        id,
                        j,
                        null,
                        null,
                        null,
                        new()));
                }
            }
        }
        // -- Debug

        return (new GoapPlan(actions, 0), nodes, debug);
    }

    /// <summary>
    /// Expands a single search node by testing a prefiltered task list.
    /// </summary>
    /// <param name="current">The current search node.</param>
    /// <param name="candidates">The task subset to evaluate.</param>
    /// <param name="debugInfo">Debug information.</param>
    private void ExpandCandidates(
        Node current,
        GoapStaticGraphCandidate[] candidates,
        GoapPlanDebugInfo? debugInfo)
    {
        foreach (var candidate in candidates)
        {
            // -- Debug
            GoapStateDebugDump? stateBefore = null;
            List<GoapPreconditionDebugDump>? preconditions = null;

            if (collectDebug)
            {
                stateBefore = current.State.GetStateDump();
                preconditions = new List<GoapPreconditionDebugDump>(candidate.Task.Preconditions.Count);
            }
            // -- Debug

            var preconditionsMet = true;

            for (var j = 0; j < candidate.Task.Preconditions.Count; j++)
            {
                // Conditions covered by a static edge are guaranteed by the edge itself.
                if (candidate.Edge?.SkipConditions.Contains(j) == true)
                {
                    // -- Debug
                    if (collectDebug)
                        preconditions?.Add(new(new(null, current.State.GetStateDump()), true));
                    // -- Debug
                    continue;
                }

                var condition = candidate.Task.Preconditions[j];
                var key = (current.State, condition);

                if (!_conditionCache.TryGetValue(key, out var cached))
                {
                    var res = goap.CheckCondition(target, current.State, condition, out var condDump);
                    cached = new(res, condDump);
                    _conditionCache[key] = cached;
                    // -- Debug
                    if (debugInfo is { } debug)
                        debug.ConditionsChecked++;
                    // -- Debug
                }

                if (!cached.Result)
                {
                    preconditionsMet = false;

                    if (!collectDebug)
                        break;
                }

                // -- Debug
                if (collectDebug)
                    preconditions?.Add(new(cached.Dump ?? new(null, current.State.GetStateDump()), cached.Result));
                // -- Debug
            }

            if (!preconditionsMet)
            {
                // -- Debug
                debugInfo?.Nodes.Add(new(
                    NodeId: candidate.Task.Id,
                    Compound: candidate.Task.Compound,
                    Preconditions: preconditions!.ToArray(),
                    StateBefore: stateBefore!.Value,
                    StateAfter: null,
                    TaskCost: current.G,
                    Heuristic: 0,
                    AddedToOpenList: false,
                    PreconditionsMet: false,
                    InPlan: false,
                    SkipReason: "Preconditions not met"));
                // -- Debug
                continue;
            }

            // Apply effects to get the new state
            var newState = ApplyEffects(current.State, candidate.Task.Effects);

            // -- Debug
            if (debugInfo != null)
            {
                var debug = debugInfo.Value;
                debug.NodesExpanded++;
                debug.EffectsApplied += candidate.Task.Effects.Count;
            }
            // -- Debug

            // Compute cost to reach new state
            var taskCost = TaskCost(target, current.State, candidate.Task, goap);
            var newG = current.G + taskCost;

            // Skip if we already found a cheaper path to this state
            if (_bestNodes.TryGetValue(newState, out var existingNode) && existingNode.G <= newG)
            {
                // -- Debug
                debugInfo?.Nodes.Add(new(
                    NodeId: candidate.Task.Id,
                    Compound: candidate.Task.Compound,
                    Preconditions: preconditions!.ToArray(),
                    StateBefore: stateBefore!.Value,
                    StateAfter: newState.GetStateDump(),
                    TaskCost: taskCost,
                    Heuristic: Heuristic(newState, goalState),
                    AddedToOpenList: false,
                    PreconditionsMet: true,
                    InPlan: false,
                    SkipReason: $"Cheaper path already exists (existing G={existingNode.G}, new G={newG})"));
                if (debugInfo is { } debug)
                    debug.SkippedExpensiveNodes++;
                // -- Debug
                continue;
            }

            var h = Heuristic(newState, goalState);

            var newNode = new Node
            {
                State = newState,
                Parent = current,
                TaskFromParent = candidate.Task,
                TaskIdFromParent = candidate.Task.Id,
                G = newG,
                F = newG + h,
                StateBefore = new(),
                Preconditions = Array.Empty<GoapPreconditionDebugDump>(),
                H = h,
            };

            // -- Debug
            if (collectDebug)
            {
                newNode.StateBefore = stateBefore!.Value;
                newNode.Preconditions = preconditions!.ToArray();
            }
            // -- Debug

            _openSet.Enqueue(newNode, newNode.F);
            _bestNodes[newState] = newNode;

            // -- Debug
            debugInfo?.Nodes.Add(new(
                NodeId: candidate.Task.Id,
                Compound: candidate.Task.Compound,
                Preconditions: preconditions!.ToArray(),
                StateBefore: stateBefore!.Value,
                StateAfter: newState.GetStateDump(),
                TaskCost: taskCost,
                Heuristic: h,
                AddedToOpenList: true,
                PreconditionsMet: true,
                InPlan: false,
                SkipReason: null));
            // -- Debug
        }
    }
}
