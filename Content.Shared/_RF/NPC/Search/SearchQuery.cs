using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Shared._RF.NPC.Search;

/// <summary>
/// Entity query for search.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class SearchQuery
{
    /// <summary>
    /// Maximum query size.
    /// </summary>
    [DataField]
    public int Limit = 256;

    /// <summary>
    /// Returns the entities that match this query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="searcher"></param>
    public abstract HashSet<EntityUid> Query(GoapState state, IQuerySearcher searcher);
}

public abstract partial class BaseSearchQuery<T> : SearchQuery where T : BaseSearchQuery<T>
{
    public override HashSet<EntityUid> Query(GoapState state, IQuerySearcher searcher)
    {
        return searcher.Query(state, (T)this);
    }
}
