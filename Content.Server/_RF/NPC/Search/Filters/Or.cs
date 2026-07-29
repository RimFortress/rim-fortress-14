using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// A filter that is triggered one of sub-filters have been triggered.
/// </summary>
public sealed partial class Or : BaseSearchFilter<Or>
{
    [DataField(required: true)]
    public List<SearchFilter> Filters = new();
}

public sealed class OrSystem : NpcSearchFilterSystem<Or>
{
    [Dependency] private readonly SharedNpcSearcherSystem _searcher = default!;

    protected override bool Filter(GoapState state, EntityUid target, Or filter)
    {
        foreach (var fl in filter.Filters)
        {
            if (fl.Filter(state, target, _searcher))
                return true;
        }

        return false;
    }
}
