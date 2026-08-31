using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Shared._RF.NPC.Search;

/// <summary>
/// Entity search query filter.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class SearchFilter
{
    /// <summary>
    /// Will the filter's result be inverted?
    /// </summary>
    [DataField]
    public bool Invert;

    /// <summary>
    /// Checks whether the target entity should be filtered out from the search query.
    /// </summary>
    /// <param name="state">GoapState of the agent requesting the search.</param>
    /// <param name="target">Target entity.</param>
    /// <param name="searcher"></param>
    public abstract bool Filter(GoapState state, EntityUid target, IQuerySearcher searcher);
}

public abstract partial class BaseSearchFilter<T> : SearchFilter where T : BaseSearchFilter<T>
{
    public override bool Filter(GoapState state, EntityUid target, IQuerySearcher searcher)
    {
        var result = searcher.Filter(state, target, (T)this);

        if (!Invert)
            return result;

        return !result;
    }
}

/// <summary>
/// Implemented by filters that combine other filters (Or, And, ...)
/// instead of evaluating world/agent state themselves. Lets the reactive
/// index route point-invalidation from a nested filter's own type up to the
/// top-level pipeline stage the composite actually occupies — a change deep
/// inside an Or still needs to re-evaluate the Or as a whole, not just the
/// child that changed.
/// </summary>
public interface ICompositeSearchFilter
{
    IEnumerable<SearchFilter> Children { get; }
}
