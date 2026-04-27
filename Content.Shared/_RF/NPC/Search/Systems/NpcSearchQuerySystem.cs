using Content.Shared._RF.NPC.GOAP;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search queries.
/// </summary>
/// <typeparam name="T">Search query type.</typeparam>
public abstract class NpcSearchQuerySystem<T> : EntitySystem where T : BaseSearchQuery<T>
{
    protected HashSet<EntityUid> Query = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetSearchQuery<T>>(OnGetSearchQuery);
    }

    private void OnGetSearchQuery(ref GetSearchQuery<T> ev)
    {
        Query.Clear();
        GetQuery(ev.State, ev.Query);
        ev.Result = Query;
    }

    /// <summary>
    /// Saves the entities that match the search query in <see cref="Query"/>.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="query">Search query.</param>
    protected abstract void GetQuery(GoapState state, T query);
}
