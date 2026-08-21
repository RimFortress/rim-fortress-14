using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Prototypes;
using Content.Shared._RF.NPC.Search.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.Search.Filters;

/// <summary>
/// Includes a list of filters from the preset.
/// </summary>
public sealed partial class Preset : BaseSearchFilter<Preset>, ICompositeSearchFilter
{
    /// <summary>
    /// Filter preset prototype.
    /// </summary>
    [DataField(required: true)]
    public ProtoId<SearchFiltersPresetPrototype> Proto;

    public IEnumerable<SearchFilter> Children => IoCManager.Resolve<IPrototypeManager>().Index(Proto).Filters;
}

public sealed class PresetSearchFilterSystem : NpcSearchFilterSystem<Preset>
{
    protected override bool Filter(GoapState state, EntityUid target, Preset filter)
    {
        foreach (var fl in filter.Children)
        {
            if (!fl.Filter(state, target, Searcher))
                return false;
        }

        return true;
    }
}
