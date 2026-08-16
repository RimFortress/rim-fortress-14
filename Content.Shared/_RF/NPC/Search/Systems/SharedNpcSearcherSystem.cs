using System.Diagnostics.CodeAnalysis;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Components;
using Content.Shared._RF.NPC.Search.Components;
using Content.Shared._RF.NPC.Search.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that allows GOAP agents to search for entities using complex filters.
/// The main system never watches the world itself. It only does three things:
/// <list type="bullet">
/// <item>
/// Cold-starts a query the first time an agent asks for it (one full pass, unavoidable).
/// </item>
/// <item>
/// Advances candidates through Filters -> Considerations when a Query or
/// Filter system reports a match via <see cref="ReportDirty"/>.
/// </item>
/// <item>
/// Rescopes a single cached Consideration score when a Consideration
/// system reports a change via <see cref="ReportRescore"/>, without
/// touching Filters or the other Considerations at all.
/// </item>
/// </list>
///
/// Everything about *when* to report a change (movement, component changes,
/// whatever) is entirely up to the Query/Filter/Consideration systems themselves.
/// </summary>
public abstract class SharedNpcSearcherSystem : EntitySystem, IQuerySearcher
{
    [Dependency] protected readonly IPrototypeManager Proto = default!;
    [Dependency] private readonly MathCurvesSystem _mathCurves = default!;
    [Dependency] private readonly EntityQuery<SearchTrackedComponent> _trackedQuery = default!;
    [Dependency] private readonly EntityQuery<NpcSearcherComponent> _searcherQuery = default!;

    /// <summary>
    /// Agents currently holding a live result for a given query prototype.
    /// For the Query stage: a brand-new candidate isn't tracked by anyone
    /// yet (no SearchTrackedComponent), so there's nothing on the entity to
    /// consult — the Query system has to check it against every agent
    /// actively watching that prototype. This index makes that O(watchers)
    /// instead of O(all agents).
    /// </summary>
    private readonly Dictionary<ProtoId<SearchQueryPrototype>, HashSet<EntityUid>> _activeAgents = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NpcSearcherComponent, ComponentShutdown>(OnSearcherShutdown);
        SubscribeLocalEvent<SearchTrackedComponent, ComponentShutdown>(OnTrackedShutdown);
    }

    private void OnSearcherShutdown(Entity<NpcSearcherComponent> ent, ref ComponentShutdown args)
    {
        foreach (var (protoId, live) in ent.Comp.Queries)
        {
            foreach (var uid in live.Tracked)
            {
                Untrack(uid, ent.Owner, protoId, live);
            }

            _activeAgents.GetValueOrDefault(protoId)?.Remove(ent.Owner);
        }
    }

    private void OnTrackedShutdown(Entity<SearchTrackedComponent> ent, ref ComponentShutdown args)
    {
        foreach (var ((agent, protoId), _) in ent.Comp.Tracking)
        {
            if (!_searcherQuery.TryComp(agent, out var comp)
                || !comp.Queries.TryGetValue(protoId, out var live)
                || !live.Tracked.Remove(ent))
                continue;

            live.Remove(ent);
        }
    }

    #region IQuerySearcher

    public HashSet<EntityUid> Query<T>(GoapState state, T query) where T : BaseSearchQuery<T>
    {
        var ev = new GetSearchQuery<T>(query, state, new());
        RaiseLocalEvent(state.GetValue(GoapState.Owner), ref ev);
        DebugTools.Assert(ev.Result.Count <= query.Limit);
        return ev.Result;
    }

    /// <summary>
    /// Returns the entities that match to the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="query">Search query.</param>
    [PublicAPI, Pure]
    public HashSet<EntityUid> Query(GoapState state, SearchQuery query)
        => query.Query(state, this);

    public bool Filter<T>(GoapState state, EntityUid target, T filter) where T : BaseSearchFilter<T>
    {
        var ev = new GetSearchFilter<T>(filter, state, target, false);
        RaiseLocalEvent(state.GetValue(GoapState.Owner), ref ev);
        return ev.Result;
    }

    /// <summary>
    /// Checks whether the target entity should be filtered out from the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target"></param>
    /// <param name="filter">Search filter.</param>
    [PublicAPI, Pure]
    public bool Filter(GoapState state, EntityUid target, SearchFilter filter)
        => filter.Filter(state, target, this);

    /// <inheritdoc cref="Filter(GoapState, EntityUid, SearchFilter)"/>
    [PublicAPI, Pure]
    public bool Filter(GoapState state, EntityUid target, List<SearchFilter> filters)
    {
        foreach (var filter in filters)
        {
            if (filter.Filter(state, target, this))
                return true;
        }

        return false;
    }

    public float Score<T>(GoapState state, EntityUid target, T con) where T : BaseSearchConsideration<T>
    {
        var ev = new GetSearchScore<T>(con, state, target, 0f);
        RaiseLocalEvent(state.GetValue(GoapState.Owner), ref ev);
        var result = _mathCurves.Get(con.Curves, ev.Result, state.GetValue(GoapState.Owner));
        return Math.Clamp(result, 0f, 1f);
    }

    /// <summary>
    /// Returns an entity score from the search query on a scale from 0 to 1.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Entity for evaluation.</param>
    /// <param name="con">Search consideration.</param>
    [PublicAPI, Pure]
    public float Score(GoapState state, EntityUid target, SearchConsideration con)
        => con.Score(state, target, this);

    /// <inheritdoc cref="Score(GoapState, EntityUid, SearchConsideration)"/>
    [PublicAPI, Pure]
    public float Score(GoapState state, EntityUid target, List<SearchConsideration> cons)
    {
        var score = 1f;

        foreach (var con in cons)
        {
            var result = con.Score(state, target, this);

            if (result == 0)
                return 0;

            score *= result;
        }

        return score;
    }

    #endregion

    private NpcSearcherComponent.LiveSearchResult GetNewResult(
        Entity<NpcSearcherComponent> agent,
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId)
    {
        var live = new NpcSearcherComponent.LiveSearchResult();
        agent.Comp.Queries[protoId] = live;

        if (!_activeAgents.TryGetValue(protoId, out var agents))
            _activeAgents[protoId] = agents = new HashSet<EntityUid>();

        agents.Add(agent);
        SeedInitial(agent, state, protoId, live);
        return live;
    }

    /// <summary>
    /// One-off cold start: runs the Query once, then advances every result
    /// through the Filters/Considerations pipeline exactly like a normal
    /// point update starting from "just passed Query" (stage -1).
    /// </summary>
    private void SeedInitial(
        EntityUid agent,
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId,
        NpcSearcherComponent.LiveSearchResult live)
    {
        if (!Proto.Resolve(protoId, out var proto))
            return;

        foreach (var candidate in Query(state, proto.Query))
        {
            Advance(agent, state, proto, candidate, -1, live);
        }
    }

    /// <summary>
    /// Runs a candidate through Filters[fromStage+1..]. If it clears every
    /// remaining Filter, scores every Consideration once (caching each one
    /// individually for later point rescoring) and inserts it if the total
    /// is positive.
    /// </summary>
    private void Advance(
        EntityUid agent,
        GoapState state,
        SearchQueryPrototype proto,
        EntityUid candidate,
        int fromStage,
        NpcSearcherComponent.LiveSearchResult live)
    {
        var stage = fromStage;

        for (var i = fromStage + 1; i < proto.Filters.Count; i++)
        {
            if (proto.Filters[i].Filter(state, candidate, this))
            {
                // Rejected here - rests at `stage` until this Filter (or an
                // earlier one) reports a change for it.
                live.Remove(candidate);
                Track(candidate, agent, proto, stage, null, live);
                return;
            }

            stage = i;
        }

        var scores = new float[proto.Considerations.Count];
        for (var i = 0; i < scores.Length; i++)
        {
            scores[i] = Score(state, candidate, proto.Considerations[i]);
        }

        Track(candidate, agent, proto, stage, scores, live);
        live.Upsert(candidate, Product(scores));
    }

    private static float Product(float[] scores)
    {
        var total = 1f;

        foreach (var score in scores)
        {
            if (score <= 0f)
                return 0f;

            total *= score;
        }

        return total;
    }

    private void Track(
        EntityUid candidate,
        EntityUid agent,
        ProtoId<SearchQueryPrototype> protoId,
        int filterStage,
        float[]? considerationScores,
        NpcSearcherComponent.LiveSearchResult live)
    {
        var tracked = EnsureComp<SearchTrackedComponent>(candidate);
        tracked.Tracking[(agent, protoId)] = new SearchTrackEntry(filterStage, considerationScores);
        live.Tracked.Add(candidate);
    }

    private void Untrack(
        EntityUid candidate,
        EntityUid agent,
        ProtoId<SearchQueryPrototype> protoId,
        NpcSearcherComponent.LiveSearchResult live)
    {
        live.Tracked.Remove(candidate);

        if (!TryComp(candidate, out SearchTrackedComponent? tracked))
            return;

        tracked.Tracking.Remove((agent, protoId));

        if (tracked.Tracking.Count == 0)
            RemComp<SearchTrackedComponent>(candidate);
    }

    #region API

    [PublicAPI, Pure]
    public static bool TryGetTracker(
        SearchTrackedComponent comp,
        (EntityUid, ProtoId<SearchQueryPrototype>) key,
        out SearchTrackEntry tracker)
        => comp.Tracking.TryGetValue(key, out tracker);

    [PublicAPI, Pure]
    public static bool TryGetLiveResult(
        NpcSearcherComponent comp,
        ProtoId<SearchQueryPrototype> protoId,
        [NotNullWhen(true)] out NpcSearcherComponent.LiveSearchResult? live)
        => comp.Queries.TryGetValue(protoId, out live);

    /// <summary>
    /// Reported by a Query or Filter system when its own local condition
    /// changed for some entities, for a specific agent + query prototype.
    /// </summary>
    /// <param name="agent">Agent whose result may be affected.</param>
    /// <param name="protoId">Query prototype the reporting stage belongs to.</param>
    /// <param name="stage">
    /// -1 if the report comes from the Query stage, or the index of the
    /// Filter in <see cref="SearchQueryPrototype.Filters"/> that is reporting.
    /// Only used for <paramref name="added"/> — tells the pipeline where to
    /// resume checking from.
    /// </param>
    /// <param name="added">
    /// Entities that newly satisfy this stage. Pushed through
    /// Filters[stage+1..] and Considerations, and inserted if everything clears.
    /// </param>
    /// <param name="removed">
    /// Entities that no longer satisfy this stage. Pulled out of the result
    /// immediately, regardless of what stage they'd previously reached.
    /// </param>
    [PublicAPI]
    public void ReportDirty(
        EntityUid agent,
        ProtoId<SearchQueryPrototype> protoId,
        int stage = -1,
        HashSet<EntityUid>? added = null,
        HashSet<EntityUid>? removed = null)
    {
        if (!TryComp(agent, out GoapComponent? goap) || !Proto.Resolve(protoId, out var proto))
            return;

        if (!TryComp(agent, out NpcSearcherComponent? comp) || !comp.Queries.TryGetValue(protoId, out var live))
            return; // agent isn't currently watching this query - nothing to update

        if (removed != null)
        {
            foreach (var uid in removed)
            {
                live.Remove(uid);
                Untrack(uid, agent, protoId, live);
            }
        }

        if (added == null)
            return;

        foreach (var uid in added)
        {
            if (!TerminatingOrDeleted(uid))
                Advance(agent, goap.State, proto, uid, stage, live);
        }
    }

    /// <summary>
    /// Reported by a Consideration system when its own score for some
    /// entities changed, for a specific agent + query prototype. Only
    /// affects entities that already cleared every Filter — replaces just
    /// this one cached consideration score and re-multiplies, without
    /// touching Filters or any other Consideration.
    /// </summary>
    /// <param name="agent">Agent whose result may be affected.</param>
    /// <param name="protoId">Query prototype the reporting consideration belongs to.</param>
    /// <param name="considerationIndex">Index into <see cref="SearchQueryPrototype.Considerations"/>.</param>
    /// <param name="changed">Entities whose score for this consideration changed.</param>
    [PublicAPI]
    public void ReportRescore(
        Entity<NpcSearcherComponent?> agent,
        ProtoId<SearchQueryPrototype> protoId,
        int considerationIndex,
        HashSet<EntityUid> changed)
    {
        if (!TryComp(agent, out GoapComponent? goap)
            || !Resolve(agent, ref agent.Comp)
            || !Proto.Resolve(protoId, out var proto))
            return;

        if (!agent.Comp.Queries.TryGetValue(proto, out var live))
            return;

        foreach (var uid in changed)
        {
            if (TerminatingOrDeleted(uid))
                continue;

            if (!_trackedQuery.TryComp(uid, out var tracked)
                || !tracked.Tracking.TryGetValue((agent, proto), out var entry)
                || entry.ConsiderationScores == null)
                continue; // not filter-complete yet - it'll get a fresh full score once it clears the Filters

            entry.ConsiderationScores[considerationIndex] =
                Score(goap.State, uid, proto.Considerations[considerationIndex]);

            live.Upsert(uid, Product(entry.ConsiderationScores));
        }
    }

    /// <summary>
    /// Returns every agent currently watching this query prototype. Intended
    /// for a Query-stage system reacting to a world event: check the new/
    /// changed entity against each of these agents' conditions and call
    /// <see cref="ReportDirty"/> for the ones it now matches.
    /// </summary>
    [PublicAPI, Pure]
    public IReadOnlySet<EntityUid> GetActiveAgents(ProtoId<SearchQueryPrototype> protoId)
        => _activeAgents.TryGetValue(protoId, out var agents) ? agents : new HashSet<EntityUid>();

    /// <summary>
    /// Returns all entities matching the search query, sorted from best to worst.
    /// </summary>
    /// <remarks>
    /// Only ever does a full pass once, the first time an agent asks for a
    /// given prototype. After that, the result is maintained purely by
    /// <see cref="ReportDirty"/>/<see cref="ReportRescore"/> calls from the
    /// individual Query/Filter/Consideration systems — a read here is just
    /// handing back the cached Sorted list.
    /// </remarks>
    [PublicAPI]
    public IReadOnlyList<EntityUid> GetResults(
        EntityUid agent,
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId)
    {
        var comp = EnsureComp<NpcSearcherComponent>(agent);

        if (!comp.Queries.TryGetValue(protoId, out var live))
            live = GetNewResult(new(agent, comp), state, protoId);

        return live.Results;
    }

    /// <inheritdoc cref="GetResults(EntityUid, GoapState, ProtoId{SearchQueryPrototype})"/>>
    [PublicAPI]
    public IReadOnlyList<EntityUid> GetResults(
        Entity<GoapComponent> ent,
        ProtoId<SearchQueryPrototype> protoId)
        => GetResults(ent, ent.Comp.State, protoId);

    /// <summary>
    /// Returns the number of results for a search query.
    /// </summary>
    /// <param name="agent">Agent entity.</param>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="protoId">Search query prototype.</param>
    [PublicAPI]
    public int GetResultsCount(
        EntityUid agent,
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId)
    {
        var comp = EnsureComp<NpcSearcherComponent>(agent);

        if (!comp.Queries.TryGetValue(protoId, out var live))
            live = GetNewResult(new(agent, comp), state, protoId);

        return live.Count;
    }

    /// <inheritdoc cref="GetResultsCount(EntityUid, GoapState, ProtoId{SearchQueryPrototype})"/>>
    [PublicAPI]
    public int GetResultsCount(
        Entity<GoapComponent> ent,
        ProtoId<SearchQueryPrototype> protoId)
        => GetResultsCount(ent, ent.Comp.State, protoId);


    /// <summary>
    /// Returns the most relevant entity for the given search query.
    /// </summary>
    /// <param name="agent">Agent entity.</param>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="protoId">Search query prototype.</param>
    /// <param name="result">Best entity.</param>
    /// <returns>True, if the entity found; otherwise, false</returns>
    [PublicAPI, Pure]
    public bool TryGetBestResult(
        EntityUid agent,
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId,
        [NotNullWhen(true)] out EntityUid? result)
    {
        result = null;

        var results = GetResults(agent, state, protoId);

        if (results.Count == 0)
            return false;

        result = results[0];
        return true;
    }

    /// <summary>
    /// Returns the most relevant entity for the given search query.
    /// </summary>
    /// <param name="ent">Agent entity.</param>
    /// <param name="protoId">Search query prototype.</param>
    /// <param name="result">Best entity.</param>
    /// <returns>True, if the entity found; otherwise, false</returns>
    [PublicAPI, Pure]
    public bool TryGetBestResult(
        Entity<GoapComponent> ent,
        ProtoId<SearchQueryPrototype> protoId,
        [NotNullWhen(true)] out EntityUid? result)
        => TryGetBestResult(ent, ent.Comp.State, protoId, out result);

    #endregion
}

public interface IQuerySearcher
{
    HashSet<EntityUid> Query<T>(GoapState state, T query) where T : BaseSearchQuery<T>;

    bool Filter<T>(GoapState state, EntityUid target, T filter) where T : BaseSearchFilter<T>;

    float Score<T>(GoapState state, EntityUid target, T con) where T : BaseSearchConsideration<T>;
}
