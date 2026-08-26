using Content.Shared._RF.NPC.GOAP.Systems;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Shared._RF.NPC.GOAP.Conditions;

/// <summary>
/// Returns true if the entity both has OpenableComponent and is not opened.
/// </summary>
public sealed partial class ItemClosed : BaseGoapCondition<ItemClosed>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField(required: true)]
    public StateKey<EntityUid> TargetKey;
}

public sealed class ItemClosedGoapConditionSystem : GoapConditionSystem<ItemClosed>
{
    [Dependency] private readonly OpenableSystem _openable = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, ItemClosed condition)
        => TryGet(state, condition.TargetKey, out var target)
           && _openable.IsClosed(target);
}
