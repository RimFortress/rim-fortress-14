using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;

namespace Content.Server._RF.NPC.Search.Filters.Farming;

/// <summary>
/// Filters plant holders based on specified parameters.
/// </summary>
public sealed partial class PlantHolder : BaseSearchFilter<PlantHolder>
{
    [DataField]
    public bool? Harvest;

    [DataField]
    public bool? Dead;

    [DataField]
    public bool? Filled;

    [DataField]
    public bool? NeedSharp;

    [DataField]
    public bool? Sampled;
}

public sealed class PlantHolderFilterSystem : NpcSearchFilterSystem<PlantHolder>
{
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    protected override bool Filter(GoapState state, EntityUid target, PlantHolder filter)
        => _query.TryComp(target, out var comp)
           && (filter.Harvest == null || filter.Harvest == comp.Harvest)
           && (filter.Dead == null || filter.Dead == comp.Dead)
           && (filter.Filled == null || filter.Filled == (comp.Seed != null))
           && (filter.NeedSharp == null || filter.NeedSharp == comp.Seed is { Ligneous: true })
           && (filter.Sampled == null || filter.Dead == comp.Sampled);
}
