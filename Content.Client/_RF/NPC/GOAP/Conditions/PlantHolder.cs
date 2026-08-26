using Content.Shared._RF.NPC.GOAP;
using Content.Shared._RF.NPC.GOAP.Systems;

namespace Content.Client._RF.NPC.GOAP.Conditions;

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

public sealed class PlantHolderConditionSystem : GoapConditionSystem<PlantHolder>
{
    protected override bool ConditionCheck(EntityUid uid, GoapState state, PlantHolder condition) => false;
}
