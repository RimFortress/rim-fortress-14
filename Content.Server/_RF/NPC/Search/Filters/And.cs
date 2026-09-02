using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// A filter that is triggered only after all its sub-filters have been triggered.
/// </summary>
public sealed partial class And : BaseSearchFilter<And>, ICompositeSearchFilter
{
    [DataField(required: true)]
    public List<SearchFilter> Filters = new();

    [ViewVariables]
    public IEnumerable<SearchFilter> Children => Filters;
}

public sealed partial class AndSystem : NpcSearchFilterSystem<And>
{
    protected override bool Filter(GoapState state, EntityUid target, And filter)
    {
        foreach (var fl in filter.Filters)
        {
            if (!fl.Filter(state, target, Searcher))
                return false;
        }

        return true;
    }
}
