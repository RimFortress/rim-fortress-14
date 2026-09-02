using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared._RF.Workshops.Components;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether the workshop is currently in production.
/// </summary>
public sealed partial class WorkshopCrafting : BaseGoapCondition<WorkshopCrafting>
{
    /// <summary>
    /// Target workshop entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed partial class WorkshopCraftingGoapConditionSystem : GoapConditionSystem<WorkshopCrafting>
{
    [Dependency] private readonly EntityQuery<WorkshopComponent> _query = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, WorkshopCrafting condition)
        => TryGet(state, condition.TargetKey, out var target)
           && _query.TryComp(target, out var comp)
           && comp.Crafting;
}
