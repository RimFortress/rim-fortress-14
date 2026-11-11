using Content.Shared._RF.Combat;
using Content.Shared._RF.NPC.Pathfinding.Conditions;
using System.Threading.Tasks;
using System.Linq;
using System.Numerics;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.Pathfinding.Algorithms;
using Content.Shared._RF.NPC.Pathfinding.Components;
using Content.Shared._RF.NPC.Pathfinding.Prototypes;
using Content.Shared.Weapons.Melee;
using JetBrains.Annotations;
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
    [Dependency] private CombatStatsSystem _combat = null!;

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

    // Whether the bodies with each tag are obstacles, see PathTagPrototype.MakesObstacle.
    private readonly List<bool> _pathTagMakesObstacle = new();

    // The dynamic tags: not baked, found out for a node when the path is searched for.
    private readonly Dictionary<ProtoId<PathTagPrototype>, PathDynamicTag> _dynamicTags = new();

    // Whether the rules refer to a dynamic tag, which makes them dynamic: they are evaluated for each node, and not once per facts.
    private readonly Dictionary<PathfindingRule, bool> _dynamicRules = new();

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
        _pathTagMakesObstacle.Clear();
        _dynamicTags.Clear();
        _dynamicRules.Clear();
        MaxTagRadius = 0f;

        foreach (var tag in _proto.EnumeratePrototypes<PathTagPrototype>().OrderBy(x => x.ID, StringComparer.Ordinal))
        {
            // The dynamic tags are not a part of the navigation map: no index, no facts.
            if (tag.Dynamic)
            {
                var shape = SampleShape(tag.InfluenceCurve, tag.Influence);
                _dynamicTags.Add(tag.ID, new PathDynamicTag(tag.ID, shape, tag.Influence));
                continue;
            }

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

            var samples = SampleShape(tag.InfluenceCurve, steps * DistanceStep);

            _pathTagIndices.Add(tag.ID, _pathTagIndices.Count);
            _pathTagInfluence.Add(samples);
            _pathTagMakesObstacle.Add(tag.MakesObstacle);
            MaxTagRadius = MathF.Max(MaxTagRadius, (samples.Length - 1) * DistanceStep);
        }

        FindDynamicSources();
    }

    /// <summary>
    /// Returns the index of the tag, which is used in <see cref="PathNodeFacts"/>.
    /// </summary>
    [PublicAPI, Pure]
    public bool TryGetTagIndex(ProtoId<PathTagPrototype> tag, out int index) => _pathTagIndices.TryGetValue(tag, out index);

    /// <summary>
    /// Whether the solid bodies that have the tag are obstacles to keep away from, see
    /// <see cref="PathTagPrototype.MakesObstacle"/>.
    /// </summary>
    [PublicAPI, Pure]
    public bool TagMakesObstacle(int index) => _pathTagMakesObstacle[index];

    /// <summary>
    /// Returns the radius in tiles within which the tag influences nodes.
    /// </summary>
    [PublicAPI, Pure]
    public float GetTagRadius(int index) => (_pathTagInfluence[index].Length - 1) * DistanceStep;

    /// <summary>
    /// Returns how strong the influence of the tag is by the distance from its source, in steps of <see cref="DistanceStep"/>. It is
    /// the shape of a field, which a field of a profile can have as well (<see cref="PathAvoidSource.Tag"/>).
    /// </summary>
    [PublicAPI, Pure]
    public float[] GetTagShape(int index) => _pathTagInfluence[index];

    /// <summary>
    /// Makes the shape of a field of the curves: how strong it is by the distance, the input of the curves, the radius being their
    /// variable <c>radius</c>.
    /// </summary>
    /// <param name="curves">The curves, the strength by the distance in tiles.</param>
    /// <param name="radius">The radius of the field.</param>
    [PublicAPI]
    public float[] SampleShape(List<MathHelpers.MathCurve.MathCurve> curves, float radius)
    {
        var samples = new float[(int) MathF.Ceiling(radius / DistanceStep) + 1];
        var variables = new Dictionary<string, float> { ["radius"] = radius };

        // Without a curve the influence is full within the radius.
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = curves.Count == 0 ? 1f : _curves.Get(curves, input: i * DistanceStep, user: null, variables: variables);
        }

        return samples;
    }

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
    /// agent's <see cref="PathProfilesComponent"/>. An agent can have only one request at a time. The query is what the
    /// request is for, the rules of the profile can apply to some queries and not to the others.
    /// </summary>
    [PublicAPI]
    public bool BeginRequest(
        Entity<PathProfilesComponent, GoapComponent> agent,
        ProtoId<PathProfilePrototype> profileId,
        ProtoId<PathQueryPrototype>? query = null)
    {
        if (!_proto.TryIndex(profileId, out var profile))
            return false;

        var comp = agent.Comp1;
        comp.ActiveProfile = profileId;
        comp.ActiveQuery = query;

        comp.Attacks.Clear();
        _combat.CollectCarried(new Entity<GoapComponent>(agent.Owner, agent.Comp2), comp.Attacks);

        // Agent conditions do not depend on the node, so they are checked once per request.
        comp.ActiveRules.Clear();
        comp.ActiveDynamicRules.Clear();

        foreach (var rule in profile.Rules)
        {
            if (!_goap.CheckCondition(agent.Owner, agent.Comp2.State, rule.AgentConditions))
                continue;

            (IsDynamic(rule) ? comp.ActiveDynamicRules : comp.ActiveRules).Add(rule);
        }

        comp.Dynamic = comp.ActiveDynamicRules.Count == 0 ? null : FindSources(agent.Owner, comp.ActiveDynamicRules);

        return true;
    }

    #region Dynamic tags

    /// <summary>
    /// Does the rule refer to a dynamic tag, so that it is not up to the facts of the node alone.
    /// </summary>
    private bool IsDynamic(PathfindingRule rule)
    {
        if (_dynamicTags.Count == 0)
            return false;

        if (_dynamicRules.TryGetValue(rule, out var dynamic))
            return dynamic;

        dynamic = rule.Conditions.Any(RefersToDynamicTag);
        _dynamicRules[rule] = dynamic;
        return dynamic;
    }

    private bool RefersToDynamicTag(PathCondition condition) => condition switch
    {
        TagInfluence influence => _dynamicTags.ContainsKey(influence.Tag),
        HasTag has => _dynamicTags.ContainsKey(has.Tag),
        Or or => or.Conditions.Any(RefersToDynamicTag),
        _ => false,
    };

    /// <summary>
    /// The conditions of the rules that refer to dynamic tags, including those inside the other conditions.
    /// </summary>
    private IEnumerable<PathCondition> DynamicConditionsOf(IEnumerable<PathCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            switch (condition)
            {
                case TagInfluence influence when _dynamicTags.ContainsKey(influence.Tag):
                case HasTag has when _dynamicTags.ContainsKey(has.Tag):
                    yield return condition;
                    break;
                case Or or:
                    foreach (var inner in DynamicConditionsOf(or.Conditions))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Finds where the sources of the dynamic tags that the rules refer to are, and what each counts for according to the rule.
    /// </summary>
    private PathDynamicInfluence? FindSources(EntityUid agent, List<PathfindingRule> rules)
    {
        if (Transform(agent).GridUid is not { } graph)
            return null;

        var inverse = _transform.GetInvWorldMatrix(graph);
        var dynamic = new PathDynamicInfluence(graph);

        foreach (var condition in rules.SelectMany(x => DynamicConditionsOf(x.Conditions)))
        {
            switch (condition)
            {
                case HasTag has:
                    dynamic.Positions[has.Tag] = Positions(has.Tag);
                    break;
                case TagInfluence influence:
                    dynamic.Positions.TryAdd(influence.Tag, Positions(influence.Tag));
                    dynamic.Sources[influence] = Sources(influence);
                    break;
            }
        }

        return dynamic;

        List<Vector2> Positions(ProtoId<PathTagPrototype> tag)
        {
            var positions = new List<Vector2>();

            foreach (var uid in _dynamicTags[tag].Sources)
            {
                if (!TerminatingOrDeleted(uid) && uid != agent)
                    positions.Add(Vector2.Transform(_transform.GetWorldPosition(uid), inverse));
            }

            return positions;
        }

        List<PathDynamicInfluence.Source> Sources(TagInfluence condition)
        {
            var info = _dynamicTags[condition.Tag];
            var sources = new List<PathDynamicInfluence.Source>();

            foreach (var uid in info.Sources)
            {
                if (TerminatingOrDeleted(uid) || uid == agent)
                    continue;

                var weight = condition.Weight.Count == 0 ? 1f : _curves.Get(condition.Weight, 0f, uid);
                var radius = condition.Reach.Count == 0 ? info.Radius : _curves.Get(condition.Reach, 0f, uid);

                if (weight <= 0f || radius <= 0f)
                    continue;

                sources.Add(new PathDynamicInfluence.Source(
                    Vector2.Transform(_transform.GetWorldPosition(uid), inverse),
                    weight,
                    radius));
            }

            return sources;
        }
    }

    /// <summary>
    /// Is something that stops projectiles between two points of a grid. It is the navigation map's to tell, which the shared part
    /// does not have: nothing is in the way here.
    /// </summary>
    [PublicAPI]
    public virtual bool IsSightBlocked(EntityUid graph, Vector2 from, Vector2 to) => false;

    /// <summary>
    /// The influence of the dynamic tag on the point: the sum of the sources, each as much as it counts for and as the shape of the tag
    /// says for the distance to it, stretched to the radius of the source.
    /// </summary>
    private float GetDynamicInfluence(PathDynamicInfluence dynamic, TagInfluence condition, Vector2 point)
    {
        if (!_dynamicTags.TryGetValue(condition.Tag, out var info) || !dynamic.Sources.TryGetValue(condition, out var sources))
            return 0f;

        var influence = 0f;

        foreach (var source in sources)
        {
            var distance = Vector2.Distance(point, source.Position);

            if (distance >= source.Radius)
                continue;

            var step = (int) (distance / source.Radius * info.Radius / DistanceStep);
            var strength = info.Shape[Math.Min(step, info.Shape.Length - 1)];

            if (strength <= 0f || condition.Sight && IsSightBlocked(dynamic.Graph, source.Position, point))
                continue;

            influence += source.Weight * strength;
        }

        return influence;
    }

    /// <summary>
    /// Returns the strength of the tag's influence on the node of the context, 0 if there is none: from the facts of the node for a baked
    /// tag, from where the sources are for a dynamic one.
    /// </summary>
    [PublicAPI, Pure]
    public float GetInfluence(in PathNodeContext ctx, TagInfluence condition)
        => ctx.Dynamic is { } dynamic && _dynamicTags.ContainsKey(condition.Tag)
            ? GetDynamicInfluence(dynamic, condition, ctx.Point)
            : GetInfluence(ctx.Facts, condition.Tag);

    /// <summary>
    /// Is the source of the tag located on the node of the context. For a dynamic tag it is a source in the tile of the node.
    /// </summary>
    [PublicAPI, Pure]
    public bool HasTag(in PathNodeContext ctx, ProtoId<PathTagPrototype> tag)
    {
        if (!_dynamicTags.ContainsKey(tag))
            return HasTag(ctx.Facts, tag);

        if (ctx.Dynamic is not { } dynamic || !dynamic.Positions.TryGetValue(tag, out var positions))
            return false;

        foreach (var position in positions)
        {
            if (Vector2.Distance(ctx.Point, position) < 0.5f)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Returns the cost that the rules of the dynamic tags add to a node: they depend on where the sources are, so they are not
    /// evaluated with the facts of the node alone, and not cached for them.
    /// </summary>
    /// <param name="agent">An agent that has started a request with <see cref="BeginRequest"/>.</param>
    /// <param name="facts">Facts about the node.</param>
    /// <param name="blocked">Is the node blocked for the agent by collision.</param>
    /// <param name="graph">The grid of the node.</param>
    /// <param name="point">The point of the node, in the coordinates of the grid.</param>
    [PublicAPI]
    public float EvaluateDynamic(
        Entity<PathProfilesComponent, GoapComponent> agent,
        in PathNodeFacts facts,
        bool blocked,
        EntityUid graph,
        Vector2 point)
    {
        var comp = agent.Comp1;

        if (comp.ActiveDynamicRules.Count == 0)
            return 0f;

        var ctx = new PathNodeContext(
            new Entity<GoapComponent>(agent.Owner, agent.Comp2),
            comp.Attacks,
            facts,
            blocked,
            _variables,
            comp.ActiveQuery,
            point,
            graph,
            comp.Dynamic);

        var cost = 0f;

        foreach (var rule in comp.ActiveDynamicRules)
        {
            _variables.Clear();

            if (RuleApplies(rule, ctx))
                cost += _curves.Get(rule.Cost, input: 0f, user: agent, variables: _variables);
        }

        return cost;
    }

    #endregion

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
            _variables,
            comp.ActiveQuery);

        var modifier = 1f;
        PathGoalInfo? before = null;
        PathGoalInfo? after = null;

        foreach (var rule in comp.ActiveRules)
        {
            _variables.Clear();

            if (!RuleApplies(rule, ctx))
                continue;

            modifier += _curves.Get(rule.Cost, input: 0f, user: agent, variables: _variables);

            if (rule.Goal is not { } goal)
                continue;

            // Only the first goal of each kind is used, the order of the rules decides which one it is.
            ref var slot = ref goal.Phase == PathGoalPhase.Before ? ref before : ref after;

            slot ??= new PathGoalInfo(goal.Id, goal.TargetKey, goal.RangeKey, goal.EntityTag, goal.EntityKey, goal.EntityConditions);
        }

        return new PathNodeEval(Math.Clamp(modifier, MinCostModifier, int.MaxValue), before, after);
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
