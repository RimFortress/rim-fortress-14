using Content.Server._RF.NPC.GOAP.Systems;
using Content.Shared._RF.NPC.GOAP;
using Content.Shared.Mobs.Components;

namespace Content.Server._RF.NPC.GOAP.Conditions;

/// <summary>
/// Checks the current state of the target entity.
/// </summary>
public sealed partial class MobState : BaseGoapCondition<MobState>
{
    /// <summary>
    /// Target entity.
    /// </summary>
    [DataField]
    public StateKey<EntityUid> TargetKey = GoapState.Owner;

    /// <summary>
    /// State against which the check will proceed.
    /// </summary>
    [DataField(required: true)]
    public Shared.Mobs.MobState TargetState;
}

public sealed class MobStateConditionSystem : GoapConditionSystem<MobState>
{
    [Dependency] private readonly EntityQuery<MobStateComponent> _mobStateQuery = default!;

    protected override bool ConditionCheck(EntityUid uid, GoapState state, MobState condition)
    {
        if (!TryGetValue(state, condition, condition.TargetKey, out var target))
            return false;

        if (!_mobStateQuery.TryComp(target, out var comp))
        {
            ComponentNotFound<MobStateComponent>(state, condition, target);
            return false;
        }

        CreateDump(state, condition, $"mob state: '{comp.CurrentState}'");
        return comp.CurrentState == condition.TargetState;
    }
}
