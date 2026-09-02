using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Traits.Components;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

// TODO: move to shared

/// <summary>
/// Filters plant holders based on specified parameters.
/// </summary>
public sealed partial class PlantHolder : BaseGoapCondition<PlantHolder>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;

    [DataField]
    public bool? Harvest;

    [DataField]
    public bool? Dead;

    [DataField]
    public bool? Filled;

    [DataField]
    public bool? NeedSharp;

    [DataField]
    public bool? HasWeed;

    [DataField]
    public bool? Sampled;

    [DataField]
    public float? WaterMoreThan;

    [DataField]
    public float? WaterLessThan;
}

public sealed partial class PlantHolderConditionSystem : GoapConditionSystem<PlantHolder>
{
    [Dependency] private PlantTraySystem _plantTray = default!;
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _holderQuery = default!;
    [Dependency] private readonly EntityQuery<PlantTrayComponent> _trayQuery = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, PlantHolder condition)
        => TryGet(state, condition.TargetKey, out var target)
           && _holderQuery.TryComp(target, out var comp)
           && _trayQuery.TryComp(target, out var tray)
           && (condition.Harvest == null || condition.Harvest == comp.ReadyForHarvest)
           && (condition.Dead == null || condition.Dead == comp.Dead)
           && (condition.Filled == null || condition.Filled == _plantTray.TryGetPlant(target, out _))
           && (condition.NeedSharp == null || condition.NeedSharp == _plantTray.TryGetPlant(target, out var plant) && HasComp<PlantTraitLigneousComponent>(plant))
           && (condition.HasWeed == null || condition.HasWeed == tray.WeedLevel > 0)
           && (condition.Sampled == null || condition.Sampled == _plantTray.TryGetPlant(target, out plant) && HasComp<PlantTraitSampledComponent>(plant))
           && (condition.WaterMoreThan == null || tray.WaterLevel > condition.WaterMoreThan)
           && (condition.WaterLessThan == null || tray.WaterLevel < condition.WaterLessThan);
}
