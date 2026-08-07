using Content.Server._RF.NPC.GOAP.Systems;
using Content.Server.Botany.Components;
using Content.Shared._RF.NPC.GOAP;

namespace Content.Server._RF.NPC.GOAP.Conditions;

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

public sealed class PlantHolderConditionSystem : GoapConditionSystem<PlantHolder>
{
    [Dependency] private readonly EntityQuery<PlantHolderComponent> _query = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, PlantHolder condition)
        => TryGetValue(state, condition, condition.TargetKey, out var target)
           && _query.TryComp(target, out var comp)
           && (condition.Harvest == null || condition.Harvest == comp.Harvest)
           && (condition.Dead == null || condition.Dead == comp.Dead)
           && (condition.Filled == null || condition.Filled == (comp.Seed != null))
           && (condition.NeedSharp == null || condition.NeedSharp == comp.Seed is { Ligneous: true })
           && (condition.HasWeed == null || condition.HasWeed == comp.WeedLevel > 0)
           && (condition.Sampled == null || condition.Sampled == comp.Sampled)
           && (condition.WaterMoreThan == null || comp.WaterLevel > condition.WaterMoreThan)
           && (condition.WaterLessThan == null || comp.WaterLevel < condition.WaterLessThan);
}
