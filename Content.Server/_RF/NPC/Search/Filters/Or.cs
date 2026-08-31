using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// A filter that is triggered one of sub-filters have been triggered.
/// </summary>
public sealed partial class Or : BaseSearchFilter<Or>, ICompositeSearchFilter
{
    [DataField(required: true)]
    public List<SearchFilter> Filters = new();

    [ViewVariables]
    public IEnumerable<SearchFilter> Children => Filters;
}

public sealed class OrSystem : NpcSearchFilterSystem<Or>
{
    protected override bool Filter(GoapState state, EntityUid target, Or filter)
    {
        foreach (var fl in filter.Filters)
        {
            if (fl.Filter(state, target, Searcher))
                return true;
        }

        return false;
    }
}
