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

public sealed class AndSystem : NpcSearchFilterSystem<And>
{
    [Dependency] private readonly SharedNpcSearcherSystem _searcher = default!;

    protected override bool Filter(GoapState state, EntityUid target, And filter)
    {
        foreach (var fl in filter.Filters)
        {
            if (!fl.Filter(state, target, _searcher))
                return false;
        }

        return true;
    }
}
