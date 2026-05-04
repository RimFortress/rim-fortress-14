using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared._RF.MathHelpers.MathCurve.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Prototypes;
using JetBrains.Annotations;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that allows GOAP agents to search for entities using complex filters.
/// </summary>
public sealed class NpcSearcherSystem : EntitySystem, IQuerySearcher
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MathCurvesSystem _mathCurves = default!;

    private readonly HashSet<EntityUid> _query = new();

    public HashSet<EntityUid> Query<T>(GoapState state, T query) where T : BaseSearchQuery<T>
    {
        _query.Clear();
        var ev = new GetSearchQuery<T>(query, state, _query);
        RaiseLocalEvent(state.GetValue(GoapState.Owner), ref ev);
        DebugTools.Assert(_query.Count <= query.Limit);
        return _query;
    }

    /// <summary>
    /// Returns the entities that match to the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="query">Search query.</param>
    [PublicAPI, Pure]
    public HashSet<EntityUid> Query(GoapState state, SearchQuery query) => query.Query(state, this);

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
    public bool Filter(GoapState state, EntityUid target, SearchFilter filter) => filter.Filter(state, target, this);

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
        var result = _mathCurves.Get(con.Curves, ev.Result);
        return Math.Clamp(result, 0f, 1f);
    }

    /// <summary>
    /// Returns an entity score from the search query on a scale from 0 to 1.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Entity for evaluation.</param>
    /// <param name="con">Search consideration.</param>
    [PublicAPI, Pure]
    public float Score(GoapState state, EntityUid target, SearchConsideration con) => con.Score(state, target, this);

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

    /// <summary>
    /// Returns all entities matching the search query, sorted from best to worst.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="protoId">Search query prototype.</param>
    [PublicAPI]
    public List<EntityUid> GetResults(GoapState state, ProtoId<SearchQueryPrototype> protoId)
    {
        if (!_proto.Resolve(protoId, out var proto))
            return new();

        var query = new Dictionary<EntityUid, float>();

        foreach (var uid in Query(state, proto.Query))
        {
            if (Filter(state, uid, proto.Filters))
                continue;

            var score = Score(state, uid, proto.Considerations);
            query.Add(uid, score);
        }

        var result = query
            .OrderBy(x => x.Value)
            .Select(x => x.Key)
            .ToList();
        return result;
    }

    /// <summary>
    /// Returns the most relevant entity for the given search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="protoId">Search query prototype.</param>
    /// <param name="result">Best entity.</param>
    /// <returns>True, if the entity found; otherwise, false</returns>
    [PublicAPI, Pure]
    public bool TryGetBestResult(
        GoapState state,
        ProtoId<SearchQueryPrototype> protoId,
        [NotNullWhen(true)] out EntityUid? result)
    {
        result = null;

        var results = GetResults(state, protoId);

        if (results.Count == 0)
            return false;

        result = results[0];
        return true;
    }
}

public interface IQuerySearcher
{
    HashSet<EntityUid> Query<T>(GoapState state, T query) where T : BaseSearchQuery<T>;

    bool Filter<T>(GoapState state, EntityUid target, T filter) where T : BaseSearchFilter<T>;

    float Score<T>(GoapState state, EntityUid target, T con) where T : BaseSearchConsideration<T>;
}
