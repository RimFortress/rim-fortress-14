using Content.Shared._RF.NPC.GOAP;

namespace Content.Shared._RF.NPC.Search.Systems;

/// <summary>
/// A system that handles search query filters.
/// </summary>
/// <typeparam name="T">Search filter type.</typeparam>
public abstract class NpcSearchFilterSystem<T> : EntitySystem where T : BaseSearchFilter<T>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GetSearchFilter<T>>(OnGetSearchFilter);
    }

    private void OnGetSearchFilter(ref GetSearchFilter<T> ev)
    {
        ev.Result = Filter(ev.State, ev.Target, ev.Filter);
    }

    /// <summary>
    /// Checks whether the target entity should be filtered out from the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="filter">Search filter.</param>
    protected abstract bool Filter(GoapState state, EntityUid target, T filter);
}
