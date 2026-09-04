using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.Search;
using Content.Shared._RF.NPC.Search.Systems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Events;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;
using Content.Shared.Interaction.Events;

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

public sealed partial class PlantHolderFilterSystem : NpcSearchFilterSystem<PlantHolder>
{
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private EntityQuery<PlantHolderComponent> _query;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlantHolderComponent, ContactInteractionEvent>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<PlantTrayComponent, TrayUpdateEvent>((ent, ref _) => DirtyFilter(ent.Owner));
        SubscribeLocalEvent<PlantTrayComponent, PlantGrowEvent>((ent, ref _) => DirtyFilter(ent.Owner));
    }

    protected override bool Filter(GoapState state, EntityUid target, PlantHolder filter)
        => _query.TryComp(target, out var comp)
           && (filter.Harvest == null || filter.Harvest == comp.ReadyForHarvest)
           && (filter.Dead == null || filter.Dead == comp.Dead)
           && (filter.Filled == null || filter.Filled == _plantTray.TryGetPlant(target, out _))
           && (filter.NeedSharp == null || filter.NeedSharp == _plantTray.TryGetPlant(target, out var plant)
               && HasComp<PlantTraitLigneousComponent>(plant))
           && (filter.Sampled == null || filter.Sampled == _plantTray.TryGetPlant(target, out plant)
               && HasComp<PlantTraitSampledComponent>(plant));
}
