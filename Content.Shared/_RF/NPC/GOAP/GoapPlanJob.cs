using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared._RF.NPC.GOAP.Systems;
using Robust.Shared.CPUJob.JobQueues;

namespace Content.Shared._RF.NPC.GOAP;

/// <summary>
/// Performs GOAP planning using recursive regression (backward-chaining) search,
/// exhaustively exploring alternatives to find the lowest-cost valid plan.
/// </summary>
/// <remarks>
/// <para>
/// <b>High-level algorithm.</b> This planner works backwards from the goal:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// For every <c>(key, value)</c> pair in <paramref name="goalState"/>, look up which tasks are
/// statically known to produce that effect via <see cref="GoapStaticGraph.NodesByEffect"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// For each candidate producer, recursively make sure ITS preconditions are met first, by
/// walking backwards along <see cref="GoapStaticGraph.CandidatesByNodeId"/> (the set of tasks
/// whose effects can satisfy this task's preconditions, including tasks that could not be
/// statically linked at all.
/// </description>
/// </item>
/// <item>
/// <description>
/// Once a task's preconditions are satisfied, the task is appended to the plan and its effects
/// are folded into the simulated state, and the search proceeds to the next goal fact.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Cost-optimal, exhaustive search.</b> Unlike a greedy "first valid plan" planner, every
/// choice point in this search - which producer satisfies a given goal fact, and which subset of
/// candidates is used to unblock a task's preconditions - is fully explored rather than
/// short-circuited on the first success. Each subroutine tracks the running cost of the plan
/// built so far and only keeps the cheapest complete result:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="ResolveTask"/>/<see cref="ResolveLoop"/> return the CHEAPEST prerequisite chain
/// that satisfies a task's preconditions from a given state, considering every relevant subset
/// of that task's candidates (not just the first subset that happens to work).
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="ResolveGoal"/> tries every candidate producer for every goal fact (rather than
/// returning after the first producer that leads to a complete plan) and keeps track of the
/// cheapest complete plan found across the whole search in <see cref="_bestPlan"/> /
/// <see cref="_bestCost"/>.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Branch-and-bound pruning.</b> Because a plan's cost can only grow as more tasks are added
/// (this assumes <see cref="SharedGoapSystem.ActionCost(EntityUid, GoapState, GoapAction)"/>
/// never returns a negative value), any partially-built plan whose accumulated cost has already
/// reached or exceeded the best COMPLETE plan cost found so far can safely be abandoned. This
/// bound is checked in <see cref="ResolveGoal"/> before exploring a branch further. It is NOT
/// threaded into <see cref="ResolveTask"/>'s memoized subplan search: the cheapest way to satisfy
/// a given task from a given state is a fixed, context-independent value, and bounding it against
/// a caller-specific running cost would make the memoization cache (<see cref="_resolveCache"/>)
/// incorrect for other callers with a different running cost.
/// </para>
/// <para>
/// <b>Debug support.</b> When compiled with <c>TOOLS</c> and invoked with <c>collectDebug: true</c>,
/// this planner populates <see cref="GoapPlanDebugInfo.Nodes"/> with two kinds of entries:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <b>Search-time attempts</b> (<see cref="GoapNodeDebugEntry.InPlan"/> <c>== false</c>), logged
/// from <see cref="ResolveTask"/> (trivial "preconditions already met" cases) and
/// <see cref="ResolveLoop"/> (every candidate actually considered as a "take" option - whether it
/// was pruned as irrelevant, failed to resolve, or was successfully committed). Each entry records
/// <see cref="GoapNodeDebugEntry.FromNodeId"/> - the id of the parent task that was checking this
/// node (or <c>null</c> if it was requested directly for a top-level goal fact). Deduplication via
/// <see cref="_loggedTrivial"/> and <see cref="_loggedCandidates"/> is keyed to include this parent
/// id, so if the SAME node is checked, at the same state, by two DIFFERENT parents somewhere in the
/// exhaustive search, both perspectives are kept as separate entries - only truly redundant repeat
/// visits (same node, same state, same parent) are collapsed.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Final path entries</b> (<see cref="GoapNodeDebugEntry.InPlan"/> <c>== true</c>), produced by
/// replaying <see cref="_bestPlan"/> from the start state in <see cref="Process"/> once the search
/// is complete, one entry per task in execution order with an accurate before/after state and
/// cost.
/// </description>
/// </item>
/// </list>
/// <para>
/// <b>Handling of dynamic (entity) conditions.</b> Some preconditions reference live ECS state
/// (<see cref="GoapCondition.EntityCondition"/>) and cannot be predicted at static-graph-build
/// time. Tasks with such preconditions (or effects) are added as fallback candidates to every
/// node's candidate list. This planner does not treat them specially: every precondition, static
/// or dynamic, is checked the same way via <see cref="AllConditionsMet"/> / <see cref="CheckCached"/>.
/// The only planner-side concession to their unpredictability is the relevance pruning in
/// <see cref="ImprovesConditions"/>, which keeps their unconditional presence in every candidate
/// list from causing a combinatorial explosion of irrelevant subtrees.
/// </para>
/// </remarks>
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
    /// <summary>
    /// Hard ceiling on recursion depth for <see cref="ResolveTask"/> / <see cref="ResolveLoop"/>.
    /// Exists purely as a safety net against pathological or malformed graphs; under normal
    /// operation the cycle guard and memoization should keep recursion well below this limit.
    /// </summary>
    private const int MaxDepth = 64;

    /// <summary>
    /// Number of recursive steps to perform between cooperative-yield checks.
    /// </summary>
    private const int StepsPerYield = 256;

    /// <summary>
    /// Cached result of checking a single condition against a single state, so the same
    /// (state, condition) pair is never evaluated twice.
    /// </summary>
    private readonly record struct ConditionCacheEntry(bool Result, GoapDebugDump? Dump);

    private readonly Dictionary<(GoapState, GoapCondition), ConditionCacheEntry> _conditionCache = new();

    /// <summary>
    /// Cycle guard. Contains the ids of tasks currently being resolved somewhere up the current
    /// recursion stack. See the original remarks on this field for why cycle-guard failures are
    /// never cached in <see cref="_resolveCache"/>.
    /// </summary>
    private readonly HashSet<int> _chain = new();

    /// <summary>
    /// Memorizes the cheapest result of <see cref="ResolveTask"/> for a given (task id, state)
    /// pair, as a <c>(Plan, Cost)</c> tuple. A <c>null</c> plan with
    /// <see cref="float.PositiveInfinity"/> cost means "provably unresolvable from this state".
    /// </summary>
    private readonly Dictionary<(int TaskId, GoapState State), (List<ExecutableGoapTask>? Plan, float Cost)> _resolveCache = new();

    /// <summary>
    /// The cheapest complete plan found so far across the whole goal-resolution search, or
    /// <c>null</c> if none has been found yet.
    /// </summary>
    private List<ExecutableGoapTask>? _bestPlan;

    /// <summary>
    /// The total cost of <see cref="_bestPlan"/>, or <see cref="float.PositiveInfinity"/> if no
    /// complete plan has been found yet. Used both as the final answer and as the
    /// branch-and-bound cutoff in <see cref="ResolveGoal"/>.
    /// </summary>
    private float _bestCost = float.PositiveInfinity;

    private int _stepsSinceYield;
    private int _conditionsChecked;
    private int _nodesExpanded;

#if TOOLS // Debug
    /// <summary>
    /// Direct reference to the in-progress <see cref="GoapPlanDebugInfo.Nodes"/> list for the
    /// current planning job, or <c>null</c> if <c>collectDebug</c> is <c>false</c>. Stored as a
    /// field (rather than threading the whole <see cref="GoapPlanDebugInfo"/> through every
    /// recursive call) purely for convenience - <see cref="GoapPlanDebugInfo"/>'s <c>Nodes</c>
    /// property is a reference-type <see cref="List{T}"/>, so holding onto it here and mutating
    /// it from deep inside the recursion is equivalent to mutating the original struct's list.
    /// </summary>
    private List<GoapNodeDebugEntry>? _debugNodes;

    /// <summary>
    /// Deduplicates search-time "preconditions already met" entries logged by
    /// <see cref="ResolveTask"/>, keyed by (the task being checked, the id of the parent task that
    /// was checking it - or <c>null</c> if it was requested directly for a goal fact, the state it
    /// was checked in). The parent is included in the key deliberately: the same task at the same
    /// state can legitimately be checked by several different parent tasks across the exhaustive
    /// search (e.g. two different tasks might both list it as a candidate), and each such
    /// perspective is kept as its own debug entry via <see cref="GoapNodeDebugEntry.FromNodeId"/>
    /// rather than only the first one encountered. Without any dedup at all, however, the same
    /// (task, parent, state) triple could still be logged many times by different backtracking
    /// branches that happen to re-ask the identical question, which is what this set prevents.
    /// </summary>
    private readonly HashSet<(int TaskId, int? FromNodeId, GoapState State)> _loggedTrivial = new();

    /// <summary>
    /// Deduplicates search-time "candidate considered" entries logged by <see cref="ResolveLoop"/>,
    /// keyed by (the task whose preconditions are being resolved, the candidate being considered
    /// for it, the state the candidate was considered from). The exhaustive skip/take search can
    /// reconsider the same candidate at the same state from multiple different backtracking
    /// branches; this keeps the debug log to one entry per distinct attempt.
    /// </summary>
    private readonly HashSet<(int OwnerTaskId, int CandidateTaskId, GoapState State)> _loggedCandidates = new();
#endif

    /// <summary>
    /// Entry point for the planning job. Exhaustively resolves every fact in
    /// <see cref="goalState"/> via <see cref="ResolveGoal"/>, keeping the cheapest complete
    /// plan found, then flattens it into a concrete <see cref="GoapPlan"/>.
    /// </summary>
    protected override async Task<(GoapPlan? Plan, GoapPlanDebugInfo? Debug)> Process()
    {
        _conditionCache.Clear();
        _chain.Clear();
        _resolveCache.Clear();
        _bestPlan = null;
        _bestCost = float.PositiveInfinity;
        _conditionsChecked = 0;
        _nodesExpanded = 0;

#if TOOLS // Debug
        var debugInfo = new GoapPlanDebugInfo
        {
            StartState = startState.GetStateDump(),
            GoalState = goalState.GetStateDump(),
            Nodes = new(),
            Actions = new(),
        };

        _debugNodes = collectDebug ? debugInfo.Nodes : null;
        _loggedTrivial.Clear();
        _loggedCandidates.Clear();
#endif

        // An empty goal is trivially "already satisfied" but has no plan to offer; an empty
        // graph means there is nothing the agent even knows how to do.
        if (goalState.Count == 0 || graph.Nodes.Count == 0)
            return Return();

        var goals = new List<KeyValuePair<string, object>>(goalState);
        var startClone = startState.ShallowClone();

        await ResolveGoal(goals, 0, startClone, new List<ExecutableGoapTask>(), 0f);

        var plan = _bestPlan;

        if (plan == null)
            return Return();

        // Flatten the ordered task chain into a flat action list. The total cost was already
        // computed precisely during the search (_bestCost), so there's no need to replay costs
        // again here for that purpose - but the TOOLS build still replays the plan below to
        // produce accurate final-path debug entries.
        var actions = new List<GoapAction>();

#if TOOLS // Debug
        var runningState = startState.ShallowClone();
#endif

        for (var i = 0; i < plan.Count; i++)
        {
            var task = plan[i];
            actions.AddRange(task.Actions);

#if TOOLS // Debug
            if (collectDebug)
            {
                // Preconditions/cost/state-before must be computed against the state as it was
                // BEFORE this task's effects are applied, so do that first.
                var preconditions = BuildPreconditionDumps(task, runningState);
                var stateBefore = runningState.GetStateDump();
                var taskCost = TaskCost(runningState, task);
                runningState = ApplyEffects(runningState, task.Effects);

                debugInfo.Nodes.Add(new GoapNodeDebugEntry(
                    NodeId: task.Id,
                    FromNodeId: null,
                    Preconditions: preconditions,
                    StateBefore: stateBefore,
                    StateAfter: runningState.GetStateDump(),
                    TaskCost: taskCost,
                    PreconditionsMet: true,
                    InPlan: true,
                    HelpGoal: task.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                    IndexInPlan: i,
                    SkipReason: null));
            }

            for (var j = 0; j < task.Actions.Count; j++)
            {
                debugInfo.Actions.Add(new GoapActionDebugInfo(
                    task.Id,
                    j,
                    null,
                    null,
                    null,
                    null,
                    new()));
            }
#endif
        }

        return Return(actions);

        (GoapPlan? Plan, GoapPlanDebugInfo? Debug) Return(List<GoapAction>? planActions = null)
        {
            var goapPlan = planActions != null ? new GoapPlan(planActions, 0) : (GoapPlan?)null;
#if TOOLS // Debug
            debugInfo.Success = planActions != null;
            debugInfo.TotalCost = _bestCost;
            debugInfo.NodesExpanded = _nodesExpanded;
            debugInfo.ConditionsChecked = _conditionsChecked;
            debugInfo.ElapsedTime = StopWatch.Elapsed;
            return (goapPlan, debugInfo);
#else // Release
            return (goapPlan, null);
#endif
        }
    }

    /// <summary>
    /// Exhaustively resolves the goal state one fact at a time (regression from the goal), and
    /// records the cheapest complete plan found in <see cref="_bestPlan"/>/<see cref="_bestCost"/>
    /// as a side effect rather than returning it directly.
    /// </summary>
    /// <remarks>
    /// See the type-level remarks for the overall algorithm and the branch-and-bound cutoff this
    /// method applies. This method does not itself log search-time debug entries - producer
    /// choices that make it into the winning plan are captured by the final-path replay in
    /// <see cref="Process"/>; only <see cref="ResolveTask"/>/<see cref="ResolveLoop"/> log
    /// individual search-time attempts, per the type-level remarks on debug support.
    /// </remarks>
    private async Task ResolveGoal(
        List<KeyValuePair<string, object>> goals,
        int index,
        GoapState state,
        List<ExecutableGoapTask> planSoFar,
        float costSoFar)
    {
        await MaybeYield();

        if (costSoFar >= _bestCost)
            return;

        if (index >= goals.Count)
        {
            if (costSoFar < _bestCost)
            {
                _bestCost = costSoFar;
                _bestPlan = new List<ExecutableGoapTask>(planSoFar);
            }

            return;
        }

        var (key, value) = goals[index];

        if (goap.TryGetValue<object>(state, key, out var current) && Equals(current, value))
        {
            await ResolveGoal(goals, index + 1, state, planSoFar, costSoFar);
            return;
        }

        if (!graph.NodesByEffect.TryGetValue((key, value), out var producers))
            return;

        foreach (var producer in producers)
        {
            // fromNodeId: null - this producer is being resolved directly for a goal fact, not
            // as a candidate of some other task.
            var (subPlan, subCost) = await ResolveTask(producer, state, 0, null);

            if (subPlan == null)
                continue;

            var stateAfterSub = ApplyPlan(state, subPlan);
            var producerCost = TaskCost(stateAfterSub, producer);
            var newCost = costSoFar + subCost + producerCost;

            if (newCost >= _bestCost)
                continue;

            var newState = ApplyEffects(stateAfterSub, producer.Effects);

            var newPlan = new List<ExecutableGoapTask>(planSoFar.Count + subPlan.Count + 1);
            newPlan.AddRange(planSoFar);
            newPlan.AddRange(subPlan);
            newPlan.Add(producer);

            await ResolveGoal(goals, index + 1, newState, newPlan, newCost);
        }
    }

    /// <summary>
    /// Finds the cheapest prerequisite chain that satisfies <paramref name="task"/>'s
    /// preconditions from <paramref name="state"/>.
    /// </summary>
    /// <param name="task">The task whose preconditions must be satisfied.</param>
    /// <param name="state">The simulated state to check/resolve preconditions against.</param>
    /// <param name="depth">Current recursion depth, used to enforce <see cref="MaxDepth"/>.</param>
    /// <param name="fromNodeId">
    /// The id of the parent task that is asking <paramref name="task"/> to be resolved (i.e. the
    /// owner task for which <paramref name="task"/> is being considered as a candidate), or
    /// <c>null</c> if <paramref name="task"/> is being resolved directly for a top-level goal fact
    /// rather than as a candidate of some other task. Purely informational for debug logging
    /// (see <see cref="GoapNodeDebugEntry.FromNodeId"/>) - it plays no role in the search or
    /// caching logic itself, since the cheapest way to resolve <c>(task, state)</c> does not
    /// depend on who is asking.
    /// </param>
    /// <remarks>
    /// <para>
    /// See the type-level remarks for the overall search strategy. Order of operations: check the
    /// trivial already-satisfied case first (logging a search-time debug entry for it, deduplicated
    /// via <see cref="_loggedTrivial"/> - keyed to include <paramref name="fromNodeId"/> so the
    /// same task/state checked by different parents is preserved as separate entries rather than
    /// collapsed into one), then the memo cache, then the cycle guard, then fall through to
    /// <see cref="ResolveLoop"/>.
    /// </para>
    /// <para>
    /// Note that the trivial check is re-evaluated on every call regardless of
    /// <see cref="_resolveCache"/> - it runs before the cache is even consulted - so a second call
    /// for the same <c>(task, state)</c> but a different <paramref name="fromNodeId"/> always gets
    /// its own fresh trivial-case log entry instead of being silently absorbed by a cache hit from
    /// the first caller.
    /// </para>
    /// </remarks>
    private async Task<(List<ExecutableGoapTask>? Plan, float Cost)> ResolveTask(
        ExecutableGoapTask task,
        GoapState state,
        int depth,
        int? fromNodeId)
    {
        if (depth > MaxDepth)
            return (null, float.PositiveInfinity);

        await MaybeYield();
        _nodesExpanded++;

        // Trivial case: nothing to resolve, this task can run right now at zero extra cost.
        if (AllConditionsMet(task, state))
        {
#if TOOLS // Debug
            if (collectDebug && _loggedTrivial.Add((task.Id, fromNodeId, state)))
            {
                _debugNodes?.Add(new GoapNodeDebugEntry(
                    NodeId: task.Id,
                    FromNodeId: fromNodeId,
                    Preconditions: BuildPreconditionDumps(task, state),
                    StateBefore: state.GetStateDump(),
                    StateAfter: null,
                    TaskCost: 0f,
                    PreconditionsMet: true,
                    InPlan: false,
                    HelpGoal: task.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                    IndexInPlan: null,
                    SkipReason: null));
            }
#endif
            return (new List<ExecutableGoapTask>(), 0f);
        }

        var cacheKey = (task.Id, state);

        if (_resolveCache.TryGetValue(cacheKey, out var cached))
            return cached;

        // Cycle guard: see _chain's remarks for why this failure is not cached.
        if (!_chain.Add(task.Id))
            return (null, float.PositiveInfinity);

        try
        {
            if (!graph.CandidatesByNodeId.TryGetValue(task.Id, out var candidates))
            {
                var none = (Plan: (List<ExecutableGoapTask>?)null, Cost: float.PositiveInfinity);
                _resolveCache[cacheKey] = none;

#if TOOLS
                if (collectDebug && _loggedTrivial.Add((task.Id, fromNodeId, state)))
                {
                    _debugNodes?.Add(new GoapNodeDebugEntry(
                        NodeId: task.Id,
                        FromNodeId: fromNodeId,
                        Preconditions: BuildPreconditionDumps(task, state),
                        StateBefore: state.GetStateDump(),
                        StateAfter: null,
                        TaskCost: 0f,
                        PreconditionsMet: false,
                        InPlan: false,
                        HelpGoal: task.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                        IndexInPlan: null,
                        SkipReason: "No nodes were found that could help satisfy the conditions of this"));
                }
#endif

                return none;
            }

#if TOOLS
            if (collectDebug && _loggedTrivial.Add((task.Id, fromNodeId, state)))
            {
                _debugNodes?.Add(new GoapNodeDebugEntry(
                    NodeId: task.Id,
                    FromNodeId: fromNodeId,
                    Preconditions: BuildPreconditionDumps(task, state),
                    StateBefore: state.GetStateDump(),
                    StateAfter: null,
                    TaskCost: 0f,
                    PreconditionsMet: false,
                    InPlan: false,
                    HelpGoal: task.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                    IndexInPlan: null,
                    SkipReason: null));
            }
#endif

            var result = await ResolveLoop(task, state, new List<ExecutableGoapTask>(), 0f, candidates, 0, depth);
            _resolveCache[cacheKey] = result;
            return result;
        }
        finally
        {
            _chain.Remove(task.Id);
        }
    }

    /// <summary>
    /// Exhaustively searches every relevant subset of a task's candidate prerequisite list for
    /// the cheapest combination that satisfies every one of <paramref name="task"/>'s
    /// preconditions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// See the type-level remarks for the exhaustive skip/take search and the relevance pruning
    /// performed by <see cref="ImprovesConditions"/>. Every candidate actually considered as a
    /// "take" option logs exactly one search-time debug entry (deduplicated via
    /// <see cref="_loggedCandidates"/>), covering three outcomes: the candidate was pruned as
    /// irrelevant, the candidate itself could not be resolved, or the candidate was successfully
    /// committed to this branch. The "skip" option is never logged, since skipping a candidate
    /// conveys no new information on its own (its absence from the log is the information).
    /// </para>
    /// </remarks>
    private async Task<(List<ExecutableGoapTask>? Plan, float Cost)> ResolveLoop(
        ExecutableGoapTask task,
        GoapState state,
        List<ExecutableGoapTask> planSoFar,
        float costSoFar,
        ExecutableGoapTask[] candidates,
        int fromIndex,
        int depth)
    {
        await MaybeYield();

        // Terminal case: preconditions already hold given everything committed so far.
        if (AllConditionsMet(task, state))
            return (planSoFar, costSoFar);

        if (fromIndex >= candidates.Length)
            return (null, float.PositiveInfinity);

        var candidate = candidates[fromIndex];
        var best = (Plan: (List<ExecutableGoapTask>?)null, Cost: float.PositiveInfinity);

        // Branch 1: don't use this candidate at all.
        var skip = await ResolveLoop(task, state, planSoFar, costSoFar, candidates, fromIndex + 1, depth);

        if (skip.Plan != null && skip.Cost < best.Cost)
            best = skip;

        // Branch 2: use this candidate, if it's actually relevant to task's unmet preconditions.
        var probeState = ApplyEffects(state, candidate.Effects);

        if (ImprovesConditions(task, state, probeState))
        {
            // fromNodeId: task.Id - candidate.Task is being resolved because `task` (the owner)
            // is checking it as a way to satisfy its own preconditions.
            var (subPlan, subCost) = await ResolveTask(candidate, state, depth + 1, task.Id);

            if (subPlan != null)
            {
                var stateAfterSub = ApplyPlan(state, subPlan);
                var candidateCost = TaskCost(stateAfterSub, candidate);
                var newCost = costSoFar + subCost + candidateCost;
                var newState = ApplyEffects(stateAfterSub, candidate.Effects);

#if TOOLS // Debug
                if (collectDebug && _loggedCandidates.Add((task.Id, candidate.Id, state)))
                {
                    _debugNodes?.Add(new GoapNodeDebugEntry(
                        NodeId: candidate.Id,
                        FromNodeId: task.Id,
                        Preconditions: BuildPreconditionDumps(candidate, stateAfterSub),
                        StateBefore: state.GetStateDump(),
                        StateAfter: newState.GetStateDump(),
                        TaskCost: candidateCost,
                        PreconditionsMet: true,
                        InPlan: false,
                        HelpGoal: candidate.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                    IndexInPlan: null,
                        SkipReason: null));
                }
#endif

                var newPlan = new List<ExecutableGoapTask>(planSoFar.Count + subPlan.Count + 1);
                newPlan.AddRange(planSoFar);
                newPlan.AddRange(subPlan);
                newPlan.Add(candidate);

                var take = await ResolveLoop(task, newState, newPlan, newCost, candidates, fromIndex + 1, depth);

                if (take.Plan != null && take.Cost < best.Cost)
                    best = take;
            }
#if TOOLS // Debug
            else if (collectDebug && _loggedCandidates.Add((task.Id, candidate.Id, state)))
            {
                _debugNodes?.Add(new GoapNodeDebugEntry(
                    NodeId: candidate.Id,
                    FromNodeId: task.Id,
                    Preconditions: BuildPreconditionDumps(candidate, state),
                    StateBefore: state.GetStateDump(),
                    StateAfter: null,
                    TaskCost: 0f,
                    PreconditionsMet: false,
                    InPlan: false,
                    HelpGoal: candidate.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                    IndexInPlan: null,
                    SkipReason: "Could not resolve this candidate's own prerequisites"));
            }
#endif
        }
#if TOOLS // Debug
        else if (collectDebug && _loggedCandidates.Add((task.Id, candidate.Id, state)))
        {
            _debugNodes?.Add(new GoapNodeDebugEntry(
                NodeId: candidate.Id,
                FromNodeId: task.Id,
                Preconditions: BuildPreconditionDumps(candidate, state),
                StateBefore: state.GetStateDump(),
                StateAfter: null,
                TaskCost: 0f,
                PreconditionsMet: false,
                InPlan: false,
                HelpGoal: candidate.Effects.Any(kv => goalState.Contains(kv.Key, kv.Value)),
                IndexInPlan: null,
                SkipReason: "Not relevant to any unmet precondition of the requesting task"));
        }
#endif

        return best;
    }

    /// <summary>
    /// Cheap pre-check used by <see cref="ResolveLoop"/> to decide whether a candidate is even
    /// worth recursively resolving: does moving from <paramref name="before"/> to
    /// <paramref name="after"/> flip at least one of <paramref name="task"/>'s currently-failing
    /// preconditions from false to true?
    /// </summary>
    private bool ImprovesConditions(ExecutableGoapTask task, GoapState before, GoapState after)
    {
        foreach (var condition in task.Preconditions)
        {
            if (!CheckCached(condition, before).Result
                && CheckCached(condition, after).Result)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks whether every one of <paramref name="task"/>'s preconditions holds in
    /// <paramref name="state"/>.
    /// </summary>
    private bool AllConditionsMet(ExecutableGoapTask task, GoapState state)
    {
        foreach (var condition in task.Preconditions)
        {
            if (!CheckCached(condition, state).Result)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks a single condition against a single state, transparently caching the result.
    /// </summary>
    private ConditionCacheEntry CheckCached(GoapCondition condition, GoapState state)
    {
        var key = (state, condition);

        if (_conditionCache.TryGetValue(key, out var cached))
            return cached;

        var result = goap.CheckCondition(target, state, condition, out var dump);
        cached = new ConditionCacheEntry(result, dump);
        _conditionCache[key] = cached;
        _conditionsChecked++;
        return cached;
    }

#if TOOLS // Debug
    /// <summary>
    /// Builds a <see cref="GoapPreconditionDebugDump"/> array for every precondition of
    /// <paramref name="task"/>, checked against <paramref name="state"/>, for inclusion in a
    /// <see cref="GoapNodeDebugEntry"/>. Uses <see cref="CheckCached"/> so building this dump
    /// never performs a redundant condition check beyond what the search itself already needed.
    /// </summary>
    private GoapPreconditionDebugDump[] BuildPreconditionDumps(ExecutableGoapTask task, GoapState state)
    {
        var result = new GoapPreconditionDebugDump[task.Preconditions.Count];

        for (var i = 0; i < task.Preconditions.Count; i++)
        {
            var entry = CheckCached(task.Preconditions[i], state);
            result[i] = new GoapPreconditionDebugDump(entry.Dump ?? new GoapDebugDump(null, state.GetStateDump()), entry.Result);
        }

        return result;
    }
#endif

    /// <summary>
    /// Cooperative-scheduling helper: yields back to the job scheduler once every
    /// <see cref="StepsPerYield"/> recursive steps.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private async Task MaybeYield()
    {
        if (++_stepsSinceYield <= StepsPerYield)
            return;

        _stepsSinceYield = 0;
        await SuspendIfOutOfTime();
    }

    /// <summary>
    /// Calculates the total cost of executing a task by summing the costs of all its constituent
    /// actions against <paramref name="state"/>.
    /// </summary>
    private float TaskCost(GoapState state, ExecutableGoapTask task)
    {
        var cost = 0f;

        foreach (var action in task.Actions)
        {
            cost += goap.ActionCost(target, state, action);
        }

        return cost;
    }

    /// <summary>
    /// Produces a new state equal to <paramref name="state"/> with every key in
    /// <paramref name="effects"/> overwritten. Never mutates <paramref name="state"/> itself.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static GoapState ApplyEffects(GoapState state, GoapState effects)
    {
        var newState = state.ShallowClone();
        newState.OverwriteFrom(effects);
        return newState;
    }

    /// <summary>
    /// Applies the effects of an ordered list of tasks to <paramref name="state"/> in sequence.
    /// </summary>
    private static GoapState ApplyPlan(GoapState state, List<ExecutableGoapTask> tasks)
    {
        var result = state;

        foreach (var task in tasks)
        {
            result = ApplyEffects(result, task.Effects);
        }

        return result;
    }
}
