using System.Threading.Tasks;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared._RF.NPC.UtilityAi.Prototypes;
using Content.Shared.Weapons.Melee;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._RF.NPC.Pathfinding.Systems;

/// <summary>
/// Path profiles: the rules by which the cost of path nodes is calculated.
/// </summary>
public abstract partial class SharedRfPathfindingSystem : EntitySystem, IPathConditionChecker, IPathAlgorithmRunner
{
    [Dependency] private IPrototypeManager _proto = null!;
    [Dependency] private MathCurvesSystem _curves = null!;
    [Dependency] private SharedGoapSystem _goap = null!;
    [Dependency] private SharedMeleeWeaponSystem _melee = null!;

    /// <summary>
    /// The cost modifier is never lower than this, otherwise the A* heuristic stops being admissible.
    /// </summary>
    private const float MinCostModifier = 0.1f;

    /// <summary>
    /// The maximum number of path tags, limited by the size of <see cref="PathInfluence.Tag"/>.
    /// </summary>
    public const int TagCapacity = byte.MaxValue;

    /// <summary>
    /// This is equivalent to agent radii for navmeshes. In our case it's preferable that things are cleanly
    /// divisible per tile so we'll make sure it works as a discrete number.
    /// </summary>
    public const byte SubStep = 4;

    /// <summary>
    /// The unit in which distances to tag sources are stored in <see cref="PathNodeFacts"/>:
    /// the distance between the breadcrumbs of the navigation map.
    /// </summary>
    public const float DistanceStep = 1f / SubStep;

    /// <summary>
    /// The maximum number of different tags that can influence a node. If there are more of them, the weakest
    /// influences are dropped.
    /// </summary>
    public const int InfluenceSlots = 8;

    public const byte ChunkSize = 8;
    public static readonly Vector2 ChunkSizeVec = new(ChunkSize, ChunkSize);

    // The maximum distance that fits into PathInfluence.Distance.
    private const int MaxDistanceSteps = byte.MaxValue;

    // Caches of the tag prototypes, rebuilt when they are reloaded.
    private readonly Dictionary<ProtoId<PathTagPrototype>, int> _pathTagIndices = new();

    // The strength of each tag's influence by the distance in steps from its source.
    private readonly List<float[]> _pathTagInfluence = new();

    // A buffer for the variables of the rule that is being evaluated.
    private readonly Dictionary<string, float> _variables = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.ProtoReload<PathTagPrototype>(_proto, BuildTagCache);
        BuildTagCache();
    }

    /// <summary>
    /// The largest radius in tiles within which a tag influences nodes.
    /// </summary>
    public float MaxTagRadius { get; private set; }

    private void BuildTagCache()
    {
        _pathTagIndices.Clear();
        _pathTagInfluence.Clear();
        MaxTagRadius = 0f;

        foreach (var tag in _proto.EnumeratePrototypes<PathTagPrototype>().OrderBy(x => x.ID, StringComparer.Ordinal))
        {
            if (_pathTagIndices.Count >= TagCapacity)
            {
                Log.Error($"Too many path tags, '{tag.ID}' and the following will be ignored.");
                break;
            }

            var steps = (int) MathF.Ceiling(tag.Influence / DistanceStep);

            if (steps > MaxDistanceSteps)
            {
                Log.Error($"Influence radius of the path tag '{tag.ID}' is too large, it will be limited.");
                steps = MaxDistanceSteps;
            }

            // Without a curve the tag influences nodes within its radius with full strength.
            var samples = new float[steps + 1];

            for (var i = 0; i < samples.Length; i++)
            {
                samples[i] = tag.InfluenceCurve.Count == 0
                    ? 1f
                    : _curves.Get(tag.InfluenceCurve, input: i * DistanceStep, user: null);
            }

            _pathTagIndices.Add(tag.ID, _pathTagIndices.Count);
            _pathTagInfluence.Add(samples);
            MaxTagRadius = MathF.Max(MaxTagRadius, (samples.Length - 1) * DistanceStep);
        }
    }

    /// <summary>
    /// Returns the index of the tag, which is used in <see cref="PathNodeFacts"/>.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetTagIndex(ProtoId<PathTagPrototype> tag, out int index) => _pathTagIndices.TryGetValue(tag, out index);

    /// <summary>
    /// Returns the radius in tiles within which the tag influences nodes.
    /// </summary>
    [PublicAPI, Pure]
    public float GetTagRadius(int index) => (_pathTagInfluence[index].Length - 1) * DistanceStep;

    #region Facts

    /// <summary>
    /// Returns the distance in tiles from the node to the nearest source of the tag.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetDistance(in PathNodeFacts facts, ProtoId<PathTagPrototype> tag, out float distance)
    {
        distance = 0f;

        if (!TryGetTagIndex(tag, out var index))
            return false;

        for (var i = 0; i < facts.InfluenceCount; i++)
        {
            if (facts.Influence[i].Tag != index)
                continue;

            distance = facts.Influence[i].Distance * DistanceStep;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Is the source of the tag located on the node itself.
    /// </summary>
    [PublicAPI, Pure]
    public bool HasTag(in PathNodeFacts facts, ProtoId<PathTagPrototype> tag)
        => TryGetDistance(facts, tag, out var distance) && distance == 0f;

    /// <summary>
    /// Returns the strength of the tag's influence on the node, 0 if there is none.
    /// </summary>
    [PublicAPI, Pure]
    public float GetInfluence(in PathNodeFacts facts, ProtoId<PathTagPrototype> tag)
    {
        if (!TryGetTagIndex(tag, out var index))
            return 0f;

        for (var i = 0; i < facts.InfluenceCount; i++)
        {
            if (facts.Influence[i].Tag == index)
                return GetInfluence(facts.Influence[i]);
        }

        return 0f;
    }

    private float GetInfluence(PathInfluence influence)
        => _pathTagInfluence[influence.Tag][influence.Distance];

    /// <summary>
    /// Marks the node as the source of the tag.
    /// </summary>
    [PublicAPI]
    public void AddTag(ref PathNodeFacts facts, ProtoId<PathTagPrototype> tag)
    {
        if (TryGetTagIndex(tag, out var index))
            AddInfluence(ref facts, index, 0f);
    }

    /// <summary>
    /// Adds the influence of the tag to the node: the node is at the given distance in tiles from the nearest
    /// source of the tag. If the node is influenced by the tag already, the closer source is kept.
    /// </summary>
    [PublicAPI]
    public void AddInfluence(ref PathNodeFacts facts, int tagIndex, float distance)
    {
        var steps = (int) MathF.Round(distance / DistanceStep);

        // Out of the tag's radius, so the tag has no influence on the node.
        if (steps < 0 || steps >= _pathTagInfluence[tagIndex].Length)
            return;

        var added = new PathInfluence((byte) tagIndex, (byte) steps);

        for (var i = 0; i < facts.InfluenceCount; i++)
        {
            if (facts.Influence[i].Tag != tagIndex)
                continue;

            if (steps < facts.Influence[i].Distance)
                facts.Influence[i] = added;

            return;
        }

        if (facts.InfluenceCount == InfluenceSlots)
        {
            // There is no room left, the weakest influence is dropped: either an existing one or the new one.
            var weakest = -1;
            var weakestStrength = GetInfluence(added);

            for (var i = 0; i < facts.InfluenceCount; i++)
            {
                var strength = GetInfluence(facts.Influence[i]);

                if (strength >= weakestStrength)
                    continue;

                weakest = i;
                weakestStrength = strength;
            }

            if (weakest < 0)
                return;

            for (var i = weakest; i < facts.InfluenceCount - 1; i++)
            {
                facts.Influence[i] = facts.Influence[i + 1];
            }

            facts.InfluenceCount--;
        }

        // The slots are kept sorted by the tag, so equal nodes have equal facts.
        var position = facts.InfluenceCount;

        while (position > 0 && facts.Influence[position - 1].Tag > tagIndex)
        {
            facts.Influence[position] = facts.Influence[position - 1];
            position--;
        }

        facts.Influence[position] = added;
        facts.InfluenceCount++;
    }

    #endregion

    #region Requests

    /// <summary>
    /// Returns the profiles of the agent that it is able to use now, in the order in which they must be tried.
    /// </summary>
    [PublicAPI, Pure]
    public List<ProtoId<PathProfilePrototype>> GetProfiles(Entity<PathProfilesComponent, GoapComponent> agent)
    {
        var result = new List<ProtoId<PathProfilePrototype>>();

        foreach (var id in agent.Comp1.Profiles)
        {
            if (_proto.TryIndex(id, out var profile)
                && _goap.CheckCondition(agent.Owner, agent.Comp2.State, profile.Conditions))
                result.Add(id);
        }

        return result;
    }

    /// <summary>
    /// Prepares the agent for a path request with the profile: the state of the request is written to the
    /// agent's <see cref="PathProfilesComponent"/>. An agent can have only one request at a time.
    /// </summary>
    [PublicAPI]
    public bool BeginRequest(Entity<PathProfilesComponent, GoapComponent> agent, ProtoId<PathProfilePrototype> profileId)
    {
        if (!_proto.TryIndex(profileId, out var profile))
            return false;

        var comp = agent.Comp1;
        comp.ActiveProfile = profileId;

        comp.Attacks.Clear();
        CollectAttacks(new Entity<GoapComponent>(agent.Owner, agent.Comp2), comp.Attacks);

        // Agent conditions do not depend on the node, so they are checked once per request.
        comp.ActiveRules.Clear();

        foreach (var rule in profile.Rules)
        {
            if (_goap.CheckCondition(agent.Owner, agent.Comp2.State, rule.AgentConditions))
                comp.ActiveRules.Add(rule);
        }

        return true;
    }

    /// <summary>
    /// Evaluates the node with the given facts using the profile of the agent's current path request.
    /// </summary>
    /// <param name="agent">An agent that has started a request with <see cref="BeginRequest"/>.</param>
    /// <param name="facts">Facts about the node.</param>
    /// <param name="blocked">Is the node blocked for the agent by collision.</param>
    [PublicAPI]
    public PathNodeEval EvaluateNode(Entity<PathProfilesComponent, GoapComponent> agent, in PathNodeFacts facts, bool blocked)
    {
        var comp = agent.Comp1;
        var ctx = new PathNodeContext(
            new Entity<GoapComponent>(agent.Owner, agent.Comp2),
            comp.Attacks,
            facts,
            blocked,
            _variables);

        var modifier = 1f;
        ProtoId<UtilityAiGoalPrototype>? goal = null;
        var goalKey = default(StateKey<EntityCoordinates>);
        var goalRangeKey = default(StateKey<float>);

        foreach (var rule in comp.ActiveRules)
        {
            _variables.Clear();

            if (!RuleApplies(rule, ctx))
                continue;

            modifier += _curves.Get(rule.Cost, input: 0f, user: agent, variables: _variables);

            // Only the first goal is used, the order of the rules decides which one it is.
            if (goal != null || rule.Goal == null)
                continue;

            goal = rule.Goal;
            goalKey = rule.GoalTargetKey;
            goalRangeKey = rule.GoalRangeKey;
        }

        return new PathNodeEval(
            Math.Clamp(modifier, MinCostModifier, int.MaxValue),
            goal,
            goalKey,
            goalRangeKey);
    }

    #endregion

    private bool RuleApplies(PathfindingRule rule, PathNodeContext ctx)
    {
        foreach (var condition in rule.Conditions)
        {
            if (!condition.Check(ctx, this))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks the path condition by raising an event that is handled by the condition's system.
    /// </summary>
    /// <param name="ctx">The node and the agent for which the path is being searched.</param>
    /// <param name="condition">Path condition.</param>
    /// <returns>True, if the check is passed; otherwise, false.</returns>
    [PublicAPI, Pure]
    public bool CheckCondition<T>(PathNodeContext ctx, T condition) where T : BasePathCondition<T>
    {
        var ev = new PathConditionCheck<T>(ctx, condition, false);
        RaiseLocalEvent(ref ev);
        return ev.Result;
    }

    /// <summary>
    /// Checks the path condition, taking its <see cref="BasePathCondition{T}.Invert"/> into account.
    /// </summary>
    /// <param name="ctx">The node and the agent for which the path is being searched.</param>
    /// <param name="condition">Path condition.</param>
    /// <returns>True, if the check is passed; otherwise, false.</returns>
    [PublicAPI, Pure]
    public bool CheckCondition(PathNodeContext ctx, PathCondition condition) => condition.Check(ctx, this);

    /// <summary>
    /// Runs the path search algorithm by raising an event that is handled by the algorithm's system.
    /// </summary>
    /// <param name="search">The input of the search.</param>
    /// <param name="algorithm">The algorithm and its settings.</param>
    /// <returns>
    /// The result of the search, which is finished when the search is done; no path if nothing handles the algorithm.
    /// </returns>
    [PublicAPI]
    public Task<PathSearchResult> Search<T>(PathSearch search, T algorithm) where T : BasePathAlgorithm<T>
    {
        var ev = new PathAlgorithmSearch<T>(search, algorithm, null);
        RaiseLocalEvent(ref ev);

        // Nobody handles this algorithm, so no path can be found.
        return ev.Result ?? Task.FromResult(PathSearchResult.NoPath);
    }
}
