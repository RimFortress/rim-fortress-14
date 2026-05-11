using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Robust.Shared.Prototypes;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks whether the target entity has the specified components.
/// </summary>
public sealed partial class HasComponent : BaseGoapCondition<HasComponent>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    [DataField(required: true)]
    public ComponentRegistry Components = new();
}

public sealed class HasComponentSystem : GoapConditionSystem<HasComponent>
{
    protected override bool ConditionCheck(EntityUid uid, GoapState state, HasComponent condition)
    {
        if (!TryGetValue(state, condition, condition.TargetKey, out var target))
            return false;

        foreach (var comp in condition.Components)
        {
            var type = comp.Value.Component.GetType();

            if (!HasComp(target, type))
            {
                ComponentNotFound(state, condition, target, type);
                return false;
            }
        }

        return true;
    }
}
